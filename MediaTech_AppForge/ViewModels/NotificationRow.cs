using System;
using System.ComponentModel;
using MediaTech_AppForge.Services;

namespace MediaTech_AppForge.ViewModels
{
    /// <summary>Une ligne de la page Notifications. IsRead notifie l'interface (mise à jour immédiate au clic).</summary>
    public class NotificationRow : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Footer { get; set; } = "";
        public bool IsGlobal { get; set; }
        public int? TargetMediaId { get; set; }
        public MediaKind? Kind { get; set; }

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead == value)
                {
                    return;
                }
                _isRead = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRead)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
