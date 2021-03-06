﻿using System.Linq;
using System.Windows;
using MahApps.Metro.SimpleChildWindow;
using TerrainEditor.Annotations;

namespace TerrainEditor.Core.Services
{
    public interface IDialogBoxService
    {
        void ShowCustomDialog(ChildWindow window, ChildWindowManager.OverlayFillBehavior behavior = ChildWindowManager.OverlayFillBehavior.WindowContent);
        MessageBoxResult ShowNativeDialog(string messageBoxText, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None);
    }

    [IsService(typeof(IDialogBoxService)), UsedImplicitly]
    public class DialogBoxService : IDialogBoxService
    {
        private static Window ActiveWindow => Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

        public void ShowCustomDialog(ChildWindow window, ChildWindowManager.OverlayFillBehavior behavior = ChildWindowManager.OverlayFillBehavior.WindowContent)
        {
            ActiveWindow.ShowChildWindowAsync(window, behavior);
        }
        public MessageBoxResult ShowNativeDialog(string messageBoxText, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            return MessageBox.Show(ActiveWindow, messageBoxText, caption, button, icon);
        }
    }
}
