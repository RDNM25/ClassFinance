using System.Windows;

namespace ClassFinance.Views.Dialogs
{
    public enum AppMessageIcon
    {
        Default,
        Database
    }

    public static class AppMessageBox
    {
        public static MessageBoxResult Show(
            Window owner,
            string message,
            string caption = "Pemberitahuan",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None,
            AppMessageIcon icon = AppMessageIcon.Default)
        {
            var dialog = new AppMessageDialog(message, caption, buttons, image, icon);
            if (owner is { IsVisible: true })
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            if (dialog.Result != MessageBoxResult.None)
                return dialog.Result;

            return buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => MessageBoxResult.Cancel
            };
        }
    }
}
