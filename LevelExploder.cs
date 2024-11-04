using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LevelDisplacer
{
    public class LevelExploder
    {
        private readonly Document _doc;
        private readonly double _spacing;
        private readonly List<Level> _levels;
        private View3D _baseView;
        private List<View3D> _levelViews;

        public LevelExploder(Document doc, List<Level> levels, double spacing)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _levels = levels?.OrderBy(l => l.Elevation).ToList() ?? throw new ArgumentNullException(nameof(levels));
            _spacing = spacing;
            _levelViews = new List<View3D>();
        }

        public bool CreateExplodedViews()
        {
            try
            {
                using (Transaction trans = new Transaction(_doc, "Create Exploded Views"))
                {
                    trans.Start();

                    // إنشاء العرض الأساسي
                    _baseView = Create3DView("Base Exploded View");
                    if (_baseView == null) return false;

                    // الحصول على حدود المبنى
                    BoundingBoxXYZ buildingBounds = GetBuildingBounds();
                    if (buildingBounds == null) return false;

                    // إنشاء عرض لكل مستوى
                    double currentSpacing = 0;
                    foreach (var level in _levels)
                    {
                        var levelView = CreateLevelView(level, buildingBounds, currentSpacing);
                        if (levelView != null)
                        {
                            _levelViews.Add(levelView);
                        }
                        currentSpacing += _spacing;
                    }

                    // إنشاء عرض مجمع
                    CreateCombinedView(buildingBounds);

                    trans.Commit();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void CreateCombinedView(BoundingBoxXYZ buildingBounds)
        {
            // إنشاء عرض مجمع
            View3D combinedView = Create3DView("Combined Exploded View");
            if (combinedView == null) return;

            // حساب الارتفاع الكلي للعرض المجمع
            double totalHeight = (_levels.Count - 1) * _spacing;
            double maxElevation = _levels.Max(l => l.Elevation);
            double minElevation = _levels.Min(l => l.Elevation);

            // تعيين صندوق القطع للعرض المجمع
            BoundingBoxXYZ combinedBox = new BoundingBoxXYZ
            {
                Min = new XYZ(
                    buildingBounds.Min.X - 10,
                    buildingBounds.Min.Y - 10,
                    minElevation - 5),
                Max = new XYZ(
                    buildingBounds.Max.X + 10,
                    buildingBounds.Max.Y + 10,
                    maxElevation + totalHeight + 5)
            };

            combinedView.SetSectionBox(combinedBox);

            // تعيين نمط العرض
            combinedView.DisplayStyle = DisplayStyle.Shading;
            combinedView.DetailLevel = ViewDetailLevel.Fine;

            // تعيين زاوية العرض الافتراضية
            SetDefaultViewOrientation(combinedView);
        }

        private void SetDefaultViewOrientation(View3D view)
        {
            XYZ eyeDirection = new XYZ(1, 1, 1).Normalize();
            XYZ upDirection = new XYZ(0, 0, 1);
            XYZ forwardDirection = (-eyeDirection).Normalize();

            // التأكد من أن المتجهات متعامدة
            upDirection = forwardDirection.CrossProduct(upDirection.CrossProduct(forwardDirection)).Normalize();

            ViewOrientation3D orientation = new ViewOrientation3D(
                eyeDirection * 500,  // أو أي مسافة تناسب عرضك
                forwardDirection,
                upDirection
            );

            view.SetOrientation(orientation);
        }


        private View3D Create3DView(string viewName)
        {
            // البحث عن نوع العرض ثلاثي الأبعاد
            ViewFamilyType viewFamilyType = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            if (viewFamilyType == null) return null;

            // إنشاء العرض
            View3D view = View3D.CreateIsometric(_doc, viewFamilyType.Id);
            view.Name = viewName;
            view.DisplayStyle = DisplayStyle.Shading;

            // تفعيل طريقة عرض المقاطع
            view.EnableRevealHiddenMode();

            return view;
        }

        private BoundingBoxXYZ GetBuildingBounds()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls),
                    new ElementCategoryFilter(BuiltInCategory.OST_Floors),
                    new ElementCategoryFilter(BuiltInCategory.OST_Roofs),
                    new ElementCategoryFilter(BuiltInCategory.OST_Columns),
                    new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                    new ElementCategoryFilter(BuiltInCategory.OST_Doors)
                }));

            BoundingBoxXYZ bounds = null;
            foreach (Element elem in collector)
            {
                BoundingBoxXYZ elemBox = elem.get_BoundingBox(null);
                if (elemBox != null)
                {
                    if (bounds == null)
                    {
                        bounds = new BoundingBoxXYZ { Min = elemBox.Min, Max = elemBox.Max };
                    }
                    else
                    {
                        bounds.Min = new XYZ(
                            Math.Min(bounds.Min.X, elemBox.Min.X),
                            Math.Min(bounds.Min.Y, elemBox.Min.Y),
                            Math.Min(bounds.Min.Z, elemBox.Min.Z));
                        bounds.Max = new XYZ(
                            Math.Max(bounds.Max.X, elemBox.Max.X),
                            Math.Max(bounds.Max.Y, elemBox.Max.Y),
                            Math.Max(bounds.Max.Z, elemBox.Max.Z));
                    }
                }
            }

            // إضافة هامش للحدود
            if (bounds != null)
            {
                bounds.Min = new XYZ(bounds.Min.X - 5, bounds.Min.Y - 5, bounds.Min.Z - 2);
                bounds.Max = new XYZ(bounds.Max.X + 5, bounds.Max.Y + 5, bounds.Max.Z + 2);
            }

            return bounds;
        }

        private View3D CreateLevelView(Level level, BoundingBoxXYZ buildingBounds, double spacing)
        {
            // إيجاد المستوى التالي
            Level nextLevel = _levels.FirstOrDefault(l => l.Elevation > level.Elevation);
            double levelHeight = nextLevel != null ?
                (nextLevel.Elevation - level.Elevation) :
                10; // ارتفاع افتراضي للطابق الأخير

            // إنشاء عرض للمستوى
            View3D levelView = Create3DView($"Level {level.Name} - Exploded View");
            if (levelView == null) return null;

            // تعيين صندوق القطع
            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ
            {
                Min = new XYZ(
                    buildingBounds.Min.X,
                    buildingBounds.Min.Y,
                    level.Elevation - 1),
                Max = new XYZ(
                    buildingBounds.Max.X,
                    buildingBounds.Max.Y,
                    level.Elevation + levelHeight + spacing)
            };

            levelView.SetSectionBox(sectionBox);
            SetDefaultViewOrientation(levelView);

            return levelView;
        }
    }
}