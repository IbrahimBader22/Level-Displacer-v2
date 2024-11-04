using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LevelDisplacer
{
    public partial class LevelDisplacerWindow : Window, INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly ExternalEvent _exEvent;
        private readonly DisplaceLevelsEventHandler _eventHandler;

        public ObservableCollection<LevelViewModel> Levels { get; private set; }

        public LevelDisplacerWindow(UIDocument uidoc, ExternalEvent exEvent, DisplaceLevelsEventHandler eventHandler)
        {
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = uidoc.Document;
            _exEvent = exEvent ?? throw new ArgumentNullException(nameof(exEvent));
            _eventHandler = eventHandler ?? throw new ArgumentNullException(nameof(eventHandler));

            Levels = new ObservableCollection<LevelViewModel>();
            InitializeComponent();
            DataContext = this;
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var settings = LevelDisplacerSettings.LoadFromFile();
                AdjustHostedCheckbox.IsChecked = settings.AdjustHosted;
                MaintainBoundingBoxCheckbox.IsChecked = settings.MaintainBoundingBox;
            }
            catch
            {
                // استخدام القيم الافتراضية في حالة الفشل
                AdjustHostedCheckbox.IsChecked = true;
                MaintainBoundingBoxCheckbox.IsChecked = true;
            }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                var settings = new LevelDisplacerSettings
                {
                    AdjustHosted = AdjustHostedCheckbox.IsChecked ?? true,
                    MaintainBoundingBox = MaintainBoundingBoxCheckbox.IsChecked ?? true
                };
                settings.SaveToFile();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnSelectLevelsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();

                try
                {
                    var levelManager = new LevelManager(_uidoc);
                    var selectedLevels = levelManager.GetSelectedLevels();

                    if (selectedLevels != null && selectedLevels.Any())
                    {
                        Levels.Clear();
                        foreach (var level in selectedLevels.OrderBy(l => l.Elevation))
                        {
                            Levels.Add(new LevelViewModel(level));
                        }
                        LevelListView.Items.Refresh();
                    }
                }
                finally
                {
                    this.Show();
                    this.Activate();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                this.Show();
                this.Activate();
            }
            catch (Exception ex)
            {
                this.Show();
                this.Activate();
                MessageBox.Show($"حدث خطأ أثناء اختيار المستويات: {ex.Message}",
                              "خطأ",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Levels.Count == 0)
                {
                    MessageBox.Show("الرجاء اختيار المستويات أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool adjustHosted = AdjustHostedCheckbox.IsChecked ?? false;
                bool maintainBoundingBox = MaintainBoundingBoxCheckbox.IsChecked ?? false;

                // تحديث الـ Handler
                _eventHandler.UpdateParameters(Levels, 0, adjustHosted);

                // تنفيذ العملية
                _exEvent.Raise();

                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء العملية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
            Close();
        }

        private void LevelListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // يمكن إضافة منطق إضافي هنا إذا كنت تريد التفاعل مع تغيير التحديد
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}