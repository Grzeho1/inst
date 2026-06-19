using System.ComponentModel;

namespace inst
{
    public class SelectableObject : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Name { get; set; } = "";
        public string Type { get; set; } = "";

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
