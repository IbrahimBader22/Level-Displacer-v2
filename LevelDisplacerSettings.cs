using System;
using System.IO;
using System.Xml.Serialization;
using System.Windows;

namespace LevelDisplacer
{
    public class LevelDisplacerSettings
    {
        private static readonly string AppDataFolder = "LevelDisplacer";
        private static readonly string SettingsFileName = "settings.xml";
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolder,
            SettingsFileName
        );

        // الإعدادات الافتراضية
        private const double DefaultDisplacementValue = 0.0;
        private const bool DefaultAdjustHosted = true;
        private const bool DefaultMaintainBoundingBox = true;

        // الخصائص القابلة للتخزين
        public double DisplacementValue { get; set; } = DefaultDisplacementValue;
        public bool AdjustHosted { get; set; } = DefaultAdjustHosted;
        public bool MaintainBoundingBox { get; set; } = DefaultMaintainBoundingBox;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string LastUser { get; set; } = Environment.UserName;

        // حدود القيم المسموح بها
        public const double MinDisplacementValue = -100000.0;
        public const double MaxDisplacementValue = 100000.0;

        public void SaveToFile()
        {
            try
            {
                // التأكد من وجود المجلد
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // تحديث معلومات الحفظ
                LastModified = DateTime.Now;
                LastUser = Environment.UserName;

                // حفظ الملف
                using (StreamWriter writer = new StreamWriter(SettingsPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(LevelDisplacerSettings));
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء حفظ الإعدادات: {ex.Message}",
                    "خطأ في حفظ الإعدادات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        public static LevelDisplacerSettings LoadFromFile()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    using (StreamReader reader = new StreamReader(SettingsPath))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(LevelDisplacerSettings));
                        var settings = (LevelDisplacerSettings)serializer.Deserialize(reader);

                        // التحقق من صحة القيم
                        settings.ValidateAndFixSettings();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"تعذر تحميل الإعدادات: {ex.Message}\nسيتم استخدام الإعدادات الافتراضية.",
                    "تنبيه",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            return new LevelDisplacerSettings();
        }

        private void ValidateAndFixSettings()
        {
            // التحقق من قيمة الإزاحة
            if (DisplacementValue < MinDisplacementValue || DisplacementValue > MaxDisplacementValue)
            {
                DisplacementValue = DefaultDisplacementValue;
            }

            // التأكد من صحة التاريخ
            if (LastModified > DateTime.Now)
            {
                LastModified = DateTime.Now;
            }

            // التأكد من وجود اسم المستخدم
            if (string.IsNullOrWhiteSpace(LastUser))
            {
                LastUser = Environment.UserName;
            }
        }

        public void ResetToDefaults()
        {
            DisplacementValue = DefaultDisplacementValue;
            AdjustHosted = DefaultAdjustHosted;
            MaintainBoundingBox = DefaultMaintainBoundingBox;
            LastModified = DateTime.Now;
            LastUser = Environment.UserName;
        }
    }
}