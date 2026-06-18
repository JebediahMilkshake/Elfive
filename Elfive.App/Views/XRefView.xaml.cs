using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Elfive.App.Views;

public partial class XRefView : UserControl
{
    public XRefView()
    {
        InitializeComponent();
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is not XRefRowViewModel row) return;
        if (row.RoutineNode is null) return;
        if (Window.GetWindow(this) is MainWindow win)
            win.NavigateToRoutineNode(row.RoutineNode, row);
    }
}
