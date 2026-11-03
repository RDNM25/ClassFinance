using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ClassFinance.Behaviors
{
    public static class CalendarPopupStyleBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CalendarPopupStyleBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not DatePicker datePicker)
                return;

            if (e.NewValue is true)
                datePicker.CalendarOpened += DatePicker_CalendarOpened;
            else
                datePicker.CalendarOpened -= DatePicker_CalendarOpened;
        }

        private static void DatePicker_CalendarOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not DatePicker datePicker)
                return;

            datePicker.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                ApplyCalendarPopupStyle(datePicker)));
        }

        private static void ApplyCalendarPopupStyle(DatePicker datePicker)
        {
            datePicker.ApplyTemplate();
            if (datePicker.Template.FindName("PART_Popup", datePicker) is not Popup popup ||
                popup.Child is not Calendar calendar)
                return;

            calendar.ApplyTemplate();
            if (calendar.Template.FindName("PART_CalendarItem", calendar) is not CalendarItem calendarItem)
                return;

            calendarItem.ApplyTemplate();
            ApplyNavigationButton(datePicker, calendarItem, "PART_PreviousButton", "‹", "Previous month");
            ApplyNavigationButton(datePicker, calendarItem, "PART_NextButton", "›", "Next month");

            if (calendarItem.Template.FindName("PART_HeaderButton", calendarItem) is not Control headerButton)
                return;

            headerButton.SetResourceReference(Control.ForegroundProperty, "TextDark");
            headerButton.SetResourceReference(TextElement.ForegroundProperty, "TextDark");
            headerButton.ApplyTemplate();
            ApplyHeaderTextForeground(headerButton);
        }

        private static void ApplyHeaderTextForeground(DependencyObject element)
        {
            if (element is TextBlock textBlock)
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextDark");

            if (element is not Visual && element is not Visual3D)
                return;

            for (var childIndex = 0; childIndex < VisualTreeHelper.GetChildrenCount(element); childIndex++)
                ApplyHeaderTextForeground(VisualTreeHelper.GetChild(element, childIndex));
        }

        private static void ApplyNavigationButton(DatePicker datePicker, CalendarItem calendarItem,
            string partName, string glyph, string accessibleName)
        {
            if (calendarItem.Template.FindName(partName, calendarItem) is not Button button)
                return;

            if (datePicker.TryFindResource("CalendarNavigationButtonTemplate") is not ControlTemplate navigationTemplate)
                return;

            button.Width = 34;
            button.Height = 34;
            button.Margin = new Thickness(4);
            button.Padding = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.FontSize = 20;
            button.FontWeight = FontWeights.SemiBold;
            button.Content = glyph;
            button.BorderThickness = new Thickness(1);
            button.SetResourceReference(Control.BackgroundProperty, "CalendarNavArrowBackground");
            button.SetResourceReference(Control.ForegroundProperty, "CalendarNavArrowForeground");
            button.SetResourceReference(Control.BorderBrushProperty, "CalendarNavArrowBorder");
            button.Template = navigationTemplate;
            AutomationProperties.SetName(button, accessibleName);
        }
    }
}
