using System.Windows;
using System.Windows.Controls;

namespace PowerCheck.App;

public partial class MainWindow
{
    private int _warningsTicked;

    private void InitLearnTab()
    {
        LearnTopics.ItemsSource = LearnContent.Topics;
        WarningList.ItemsSource = LearnContent.WarningSigns;
        ShowWarningAdvice();
    }

    private void Warning_Changed(object sender, RoutedEventArgs e)
    {
        _warningsTicked += (sender as CheckBox)?.IsChecked == true ? 1 : -1;
        _warningsTicked = Math.Max(0, _warningsTicked);
        ShowWarningAdvice();
    }

    private void ShowWarningAdvice()
    {
        if (_warningsTicked == 0)
        {
            WarningAdvice.Background = Brush("#FFFFFF");
            WarningAdviceText.Text = "Nothing ticked: good. Check again each season, and whenever you plug in something new or big.";
            return;
        }

        WarningAdvice.Background = Brush("#FFF4D6");
        WarningAdviceText.Text =
            $"You ticked {_warningsTicked}. Unplug what's involved and stop using that outlet, cord or strip for now. " +
            "Heat, smells, buzzing and scorch marks usually mean a loose or overloaded connection; replace the cord or strip, " +
            "and have an electrician look at an outlet or switch. If you see smoke or flames, get out and call 911.";
    }
}
