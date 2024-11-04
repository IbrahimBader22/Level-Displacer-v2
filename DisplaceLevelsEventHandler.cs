using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LevelDisplacer
{
    public class DisplaceLevelsEventHandler : IExternalEventHandler
    {
        private readonly Document _doc;
        private List<LevelViewModel> _levels;
        private double _spacing;
        private bool _adjustHosted;

        public DisplaceLevelsEventHandler(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public void UpdateParameters(IEnumerable<LevelViewModel> levels, double spacing, bool adjustHosted)
        {
            _levels = levels?.ToList() ?? throw new ArgumentNullException(nameof(levels));
            _spacing = spacing / 304.8; // تحويل من ملم إلى قدم
            _adjustHosted = adjustHosted;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_levels == null || !_levels.Any())
                {
                    TaskDialog.Show("تنبيه", "الرجاء اختيار المستويات أولاً.");
                    return;
                }

                using (TransactionGroup transGroup = new TransactionGroup(_doc, "Create Exploded Views"))
                {
                    transGroup.Start();

                    // إنشاء العروض
                    var levelViews = CreateLevelViews();
                    if (!levelViews.Any())
                    {
                        TaskDialog.Show("خطأ", "فشل في إنشاء العروض.");
                        return;
                    }

                    // إنشاء الشيت
                    CreateSheetWithViews(levelViews);

                    transGroup.Assimilate();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("خطأ", $"حدث خطأ غير متوقع: {ex.Message}");
            }
        }

        private List<View3D> CreateLevelViews()
        {
            var views = new List<View3D>();

            var viewFamilyType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            if (viewFamilyType == null)
            {
                TaskDialog.Show("خطأ", "لم يتم العثور على نوع العرض ثلاثي الأبعاد.");
                return views;
            }

            using (Transaction trans = new Transaction(_doc, "Create Level Views"))
            {
                trans.Start();

                foreach (var levelVM in _levels.OrderBy(l => l.RevitLevel.Elevation))
                {
                    // إنشاء عرض جديد
                    View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);
                    view.Name = $"Level {levelVM.RevitLevel.Name} - Exploded View";

                    try
                    {
                        view.Scale = 200;

                        // ضبط خصائص العرض
                        ConfigureView(view);

                        // تعيين صندوق القطع
                        var bbox = GetLevelBoundingBox(levelVM.RevitLevel);
                        if (bbox != null)
                        {
                            AdjustViewCropBox(view, levelVM.RevitLevel, bbox);
                        }

                        views.Add(view);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("خطأ", $"فشل في إعداد العرض: {ex.Message}");
                    }
                }

                trans.Commit();
            }

            return views;
        }

        private void ConfigureView(View3D view)
        {
            view.DisplayStyle = DisplayStyle.Shading;
            view.DetailLevel = ViewDetailLevel.Fine;
            SetViewOrientation(view);
            view.AreAnnotationCategoriesHidden = true;
            view.IsSectionBoxActive = true;
        }

        private void SetViewOrientation(View3D view)
        {
            XYZ eyeDirection = new XYZ(-1, -1, 1).Normalize();
            XYZ upDirection = new XYZ(0, 0, 1);
            XYZ forwardDirection = (-eyeDirection).Normalize();

            upDirection = forwardDirection.CrossProduct(upDirection.CrossProduct(forwardDirection)).Normalize();

            ViewOrientation3D orientation = new ViewOrientation3D(
                eyePosition: eyeDirection * 100,
                forwardDirection: forwardDirection,
                upDirection: upDirection
            );

            view.SetOrientation(orientation);
        }

        private void CreateSheetWithViews(List<View3D> views)
        {
            using (Transaction trans = new Transaction(_doc, "Create Sheet with Views"))
            {
                trans.Start();

                try
                {
                    // إنشاء الشيت
                    ViewSheet sheet = CreateSheet();
                    if (sheet == null) return;

                    // ترتيب وإضافة العروض
                    PlaceViewsOnSheet(sheet, views);

                    trans.Commit();
                    TaskDialog.Show("نجاح", "تم إنشاء الشيت بنجاح!");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    TaskDialog.Show("خطأ", $"فشل في إنشاء الشيت: {ex.Message}");
                }
            }
        }

        private ViewSheet CreateSheet()
        {
            // البحث عن عائلة الشيت
            FamilySymbol titleBlock = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            if (titleBlock == null)
            {
                TaskDialog.Show("خطأ", "لم يتم العثور على عائلة الشيت.");
                return null;
            }

            if (!titleBlock.IsActive) titleBlock.Activate();

            // إنشاء الشيت
            ViewSheet sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.Name = "Exploded Views";
            sheet.SheetNumber = "EX-01";

            return sheet;
        }

        private void PlaceViewsOnSheet(ViewSheet sheet, List<View3D> views)
        {
            // حساب أبعاد الشيت
            double sheetWidth = sheet.Outline.Max.U - sheet.Outline.Min.U;
            double sheetHeight = sheet.Outline.Max.V - sheet.Outline.Min.V;

            // ترتيب العروض حسب الارتفاع
            var orderedViews = views.OrderBy(v =>
            {
                var levelName = v.Name.Split('-').FirstOrDefault()?.Trim();
                return _levels.FirstOrDefault(l => levelName.Contains(l.RevitLevel.Name))?.RevitLevel.Elevation ?? 0;
            }).ToList();

            // إعداد مواقع العروض
            double centerX = (sheet.Outline.Max.U + sheet.Outline.Min.U) / 2;
            double startY = sheet.Outline.Min.V + (sheetHeight * 0.2);
            double spacing = (sheetHeight * 0.6) / (orderedViews.Count + 1);

            // وضع العروض
            for (int i = 0; i < orderedViews.Count; i++)
            {
                double y = startY + (i * spacing);
                XYZ viewLocation = new XYZ(centerX, y, 0);

                Viewport viewport = Viewport.Create(_doc, sheet.Id, orderedViews[i].Id, viewLocation);
                if (viewport != null)
                {
                    viewport.LookupParameter("Title")?.Set(GetViewTitle(orderedViews[i]));
                }
            }

            // إضافة عنوان للشيت
            AddSheetTitle(sheet, orderedViews.Count);
        }

        private string GetViewTitle(View3D view)
        {
            var levelName = view.Name.Split('-').FirstOrDefault()?.Trim();
            var level = _levels.FirstOrDefault(l => levelName.Contains(l.RevitLevel.Name))?.RevitLevel;
            return level != null ? $"Level {level.Name}" : view.Name;
        }

        private void AddSheetTitle(ViewSheet sheet, int viewCount)
        {
            try
            {
                TextNoteType textType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();

                if (textType != null)
                {
                    var options = new TextNoteOptions
                    {
                        HorizontalAlignment = HorizontalTextAlignment.Center,
                        TypeId = textType.Id
                    };

                    // عنوان رئيسي
                    TextNote.Create(_doc, sheet.Id,
                        new XYZ(sheet.Outline.Max.U * 0.5, sheet.Outline.Max.V - 0.3, 0),
                        "Building Levels - Exploded Views", options);

                    // معلومات إضافية
                    options.HorizontalAlignment = HorizontalTextAlignment.Left;
                    string info = $"Total Levels: {viewCount}";
                    TextNote.Create(_doc, sheet.Id,
                        new XYZ(sheet.Outline.Min.U + 0.3, sheet.Outline.Min.V + 0.3, 0),
                        info, options);
                }
            }
            catch { }
        }

        private void AdjustViewCropBox(View3D view, Level level, BoundingBoxXYZ bbox)
        {
            double levelHeight = GetLevelHeight(level);

            bbox.Min = new XYZ(
                bbox.Min.X - 5,
                bbox.Min.Y - 5,
                level.Elevation - 1
            );
            bbox.Max = new XYZ(
                bbox.Max.X + 5,
                bbox.Max.Y + 5,
                level.Elevation + levelHeight + 1
            );

            view.SetSectionBox(bbox);
        }

        private double GetLevelHeight(Level level)
        {
            if (level == _levels.Last().RevitLevel) return 10.0;

            var nextLevel = _levels
                .OrderBy(l => l.RevitLevel.Elevation)
                .FirstOrDefault(l => l.RevitLevel.Elevation > level.Elevation);

            return nextLevel != null ?
                nextLevel.RevitLevel.Elevation - level.Elevation :
                10.0;
        }

        private BoundingBoxXYZ GetLevelBoundingBox(Level level)
        {
            List<ElementFilter> filters = new List<ElementFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                new ElementCategoryFilter(BuiltInCategory.OST_Ceilings),
                new ElementCategoryFilter(BuiltInCategory.OST_Doors),
                new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                new ElementCategoryFilter(BuiltInCategory.OST_Columns),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming)
            };

            var collector = new FilteredElementCollector(_doc)
                .WherePasses(new ElementLevelFilter(level.Id))
                .WherePasses(new LogicalOrFilter(filters));

            return ComputeBoundingBox(collector.ToElements());
        }

        private BoundingBoxXYZ ComputeBoundingBox(IList<Element> elements)
        {
            BoundingBoxXYZ bbox = null;
            foreach (Element elem in elements)
            {
                var elemBox = elem.get_BoundingBox(null);
                if (elemBox == null) continue;

                if (bbox == null)
                {
                    bbox = new BoundingBoxXYZ
                    {
                        Min = new XYZ(elemBox.Min.X, elemBox.Min.Y, elemBox.Min.Z),
                        Max = new XYZ(elemBox.Max.X, elemBox.Max.Y, elemBox.Max.Z)
                    };
                }
                else
                {
                    bbox.Min = new XYZ(
                        Math.Min(bbox.Min.X, elemBox.Min.X),
                        Math.Min(bbox.Min.Y, elemBox.Min.Y),
                        Math.Min(bbox.Min.Z, elemBox.Min.Z)
                    );
                    bbox.Max = new XYZ(
                        Math.Max(bbox.Max.X, elemBox.Max.X),
                        Math.Max(bbox.Max.Y, elemBox.Max.Y),
                        Math.Max(bbox.Max.Z, elemBox.Max.Z)
                    );
                }
            }
            return bbox;
        }

        public string GetName() => "Level Exploder";
    }
}