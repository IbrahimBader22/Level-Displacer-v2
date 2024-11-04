using Autodesk.Revit.DB;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LevelDisplacer
{
    public class LevelViewModel : INotifyPropertyChanged
    {
        private string _name;
        private double _currentElevation;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public double CurrentElevation
        {
            get => _currentElevation;
            set
            {
                if (_currentElevation != value)
                {
                    _currentElevation = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ElevationDisplay));
                }
            }
        }

        public string ElevationDisplay => $"{CurrentElevation:F2} mm";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public Level RevitLevel { get; private set; }

        public LevelViewModel(Level level)
        {
            RevitLevel = level;
            Name = level.Name;
            // تحويل من قدم إلى ملم
            CurrentElevation = level.Elevation * 304.8;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}