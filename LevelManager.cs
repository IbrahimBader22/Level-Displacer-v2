using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Lights;

namespace LevelDisplacer
{
    public class LevelManager
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        // القيم الحدية للمناسيب (بالأقدام)
        private const double MIN_ELEVATION = -1000.0;
        private const double MAX_ELEVATION = 1000.0;

        public LevelManager(UIDocument uidoc)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
        }

        /// <summary>
        /// الحصول على المستويات المحددة من قبل المستخدم
        /// </summary>
        public List<Level> GetSelectedLevels()
        {
            try
            {
                // تحديد فلتر للمستويات فقط
                var filter = new LevelSelectionFilter();

                // السماح للمستخدم باختيار المستويات
                IList<Reference> selectedReferences = _uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    filter,
                    "الرجاء اختيار المستويات. اضغط ESC للإلغاء أو ENTER للإنهاء."
                );

                // تحويل المراجع إلى مستويات
                return selectedReferences
                    .Select(r => _doc.GetElement(r))
                    .OfType<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // إرجاع قائمة فارغة في حالة الإلغاء
                return new List<Level>();
            }
            catch (Exception ex)
            {
                throw new Exception($"حدث خطأ أثناء اختيار المستويات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// الحصول على جميع المستويات في المشروع
        /// </summary>
        public List<Level> GetAllProjectLevels()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        /// <summary>
        /// تغيير مناسيب المستويات المحددة
        /// </summary>
        public bool DisplaceLevels(IEnumerable<Level> levels, double displacementInFeet, bool adjustHosted, bool maintainBoundingBox = true)
        {
            if (!levels.Any()) return false;

            try
            {
                using (Transaction trans = new Transaction(_doc, "تغيير مناسيب المستويات"))
                {
                    trans.Start();

                    foreach (Level level in levels)
                    {
                        // حساب المنسوب الجديد
                        double newElevation = level.Elevation + displacementInFeet;

                        // التحقق من صحة المنسوب الجديد
                        if (!ValidateElevation(newElevation))
                        {
                            throw new Exception($"المنسوب الجديد {newElevation * 304.8:F2} mm خارج النطاق المسموح به.");
                        }

                        // تحديث منسوب المستوى
                        level.Elevation = newElevation;

                        if (adjustHosted)
                        {
                            AdjustHostedElements(level, displacementInFeet, maintainBoundingBox);
                        }
                    }

                    trans.Commit();
                }

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// ضبط العناصر المرتبطة بمستوى معين
        /// </summary>
        public void AdjustHostedElements(Level level, double displacementInFeet, bool maintainBoundingBox)
        {
            // جمع كل العناصر المرتبطة بالمستوى
            var hostedElements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementLevelFilter(level.Id))
                .ToElements();

            foreach (Element element in hostedElements)
            {
                try
                {
                    // التحقق من وجود علاقة ربط بالمستوى
                    Parameter levelParam = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.HasValue)
                    {
                        // تحديث الارتفاع عن المستوى
                        Parameter offsetParam = element.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                        if (offsetParam != null && offsetParam.HasValue && !offsetParam.IsReadOnly)
                        {
                            double currentOffset = offsetParam.AsDouble();
                            offsetParam.Set(currentOffset + displacementInFeet);
                        }

                    }
                }
                catch
                {
                    // تجاهل العناصر التي لا يمكن تعديلها
                    continue;
                }
            }
        }

        /// <summary>
        /// تحديث العناصر الخاصة التي تحتاج معالجة خاصة
        /// </summary>
       

        /// <summary>
        /// التحقق من صحة قيمة المنسوب
        /// </summary>
        public bool ValidateElevation(double elevationInFeet)
        {
            return elevationInFeet >= MIN_ELEVATION && elevationInFeet <= MAX_ELEVATION;
        }

        /// <summary>
        /// الحصول على المستويات في نطاق محدد
        /// </summary>
        public List<Level> GetLevelsInRange(double minElevation, double maxElevation)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .WhereElementIsNotElementType()
                .Cast<Level>()
                .Where(l => l.Elevation >= minElevation && l.Elevation <= maxElevation)
                .OrderBy(l => l.Elevation)
                .ToList();
        }
    }

    /// <summary>
    /// فلتر لاختيار المستويات فقط
    /// </summary>
    public class LevelSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Level;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}