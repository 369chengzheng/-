using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using IMDP.Application.Events;
using IMDP.Products.GasDG.BoardA.ViewModels;
using Prism.Events;

namespace IMDP.Products.GasDG.BoardA.Views;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is bool b ? !b : Binding.DoNothing;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is bool b ? !b : Binding.DoNothing;
}

public partial class BoardView : UserControl
{
    private IEventAggregator? _eventAggregator;

    public BoardView(IEventAggregator eventAggregator)
    {
        InitializeComponent();
        _eventAggregator = eventAggregator;
        _eventAggregator.GetEvent<RebuildStationColumnsEvent>().Subscribe(OnRebuildStationColumns);
    }

    private void OnRebuildStationColumns(int count)
    {
        Dispatcher.BeginInvoke(new Action(() => RebuildStationColumns(count)),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RebuildStationColumns(int count)
    {
        for (int i = DetectionDataGrid.Columns.Count - 1; i >= 2; i--)
        {
            DetectionDataGrid.Columns.RemoveAt(i);
        }

        for (int i = 0; i < count; i++)
        {
            var column = new DataGridTemplateColumn
            {
                Header = $"工位{i + 1}",
                Width = new DataGridLength(120)
            };

            column.CellTemplate = CreateCellTemplate(i);

            DetectionDataGrid.Columns.Add(column);
        }
    }

    private DataTemplate CreateCellTemplate(int index)
    {
        var template = new DataTemplate(typeof(BoardDetectionRowDto));

        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));
        var backgroundBinding = new Binding($"CellValues[{index}].BackgroundBrush");
        borderFactory.SetBinding(Border.BackgroundProperty, backgroundBinding);

        var contentControlFactory = new FrameworkElementFactory(typeof(ContentControl));
        contentControlFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        contentControlFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        contentControlFactory.SetValue(ContentControl.ContentProperty, new Binding($"CellValues[{index}]"));
        contentControlFactory.SetValue(ContentControl.ContentTemplateSelectorProperty, new CellTemplateSelector());

        borderFactory.AppendChild(contentControlFactory);
        template.VisualTree = borderFactory;

        return template;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nestedChild = FindVisualChild<T>(child);
            if (nestedChild != null) return nestedChild;
        }
        return null;
    }
}

public class CellTemplateSelector : DataTemplateSelector
{
    private static readonly DataTemplate NormalTemplate;
    private static readonly DataTemplate SerialPortTemplate;
    private static readonly DataTemplate StatusTemplate;
    private static readonly DataTemplate ScanSnTemplate;

    static CellTemplateSelector()
    {
        NormalTemplate = CreateNormalTemplate();
        SerialPortTemplate = CreateSerialPortTemplate();
        StatusTemplate = CreateStatusTemplate();
        ScanSnTemplate = CreateScanSnTemplate();
    }

    private static DataTemplate CreateNormalTemplate()
    {
        var template = new DataTemplate(typeof(BoardCellValueDto));
        var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
        textBlockFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("Value"));
        template.VisualTree = textBlockFactory;
        return template;
    }

    private static DataTemplate CreateSerialPortTemplate()
    {
        var template = new DataTemplate(typeof(BoardCellValueDto));
        var comboBoxFactory = new FrameworkElementFactory(typeof(ComboBox));
        comboBoxFactory.SetValue(ComboBox.FontSizeProperty, 11.0);
        comboBoxFactory.SetValue(ComboBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        comboBoxFactory.SetBinding(ComboBox.TextProperty, new Binding("SerialPort"));
        template.VisualTree = comboBoxFactory;
        return template;
    }

    private static DataTemplate CreateStatusTemplate()
    {
        var template = new DataTemplate(typeof(BoardCellValueDto));
        var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
        textBlockFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("StatusText"));
        template.VisualTree = textBlockFactory;
        return template;
    }

    private static DataTemplate CreateScanSnTemplate()
    {
        var template = new DataTemplate(typeof(BoardCellValueDto));
        var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
        textBoxFactory.SetValue(TextBox.FontSizeProperty, 11.0);
        textBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        textBoxFactory.SetBinding(TextBox.TextProperty, new Binding("ScanSnText"));
        textBoxFactory.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(ScanSnTextBox_KeyDown));
        template.VisualTree = textBoxFactory;
        return template;
    }

    private static void ScanSnTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var textBox = sender as TextBox;
        if (textBox == null) return;

        var cell = FindParent<DataGridCell>(textBox);
        if (cell == null) return;

        var row = FindParent<DataGridRow>(cell);
        if (row == null) return;

        var dataGrid = FindParent<DataGrid>(cell);
        if (dataGrid == null) return;

        e.Handled = true;

        int currentColumnIndex = GetCellColumnIndexFromDataGrid(cell, dataGrid);
        if (currentColumnIndex < 2) return;

        int firstDataColumn = 2;
        int lastDataColumn = dataGrid.Columns.Count - 1;

        int nextColumnIndex = currentColumnIndex + 1;
        if (nextColumnIndex > lastDataColumn)
        {
            nextColumnIndex = firstDataColumn;
        }

        textBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            var nextCell = GetCellFromDataGrid(row, nextColumnIndex, dataGrid);
            if (nextCell != null)
            {
                nextCell.Focus();
                var nextTextBox = FindVisualChild<TextBox>(nextCell);
                if (nextTextBox != null)
                {
                    nextTextBox.Focus();
                    nextTextBox.SelectAll();
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private static int GetCellColumnIndexFromDataGrid(DataGridCell cell, DataGrid dataGrid)
    {
        if (cell == null || dataGrid == null) return -1;

        var presenter = FindParent<DataGridCellsPresenter>(cell);
        if (presenter != null)
        {
            var index = presenter.ItemContainerGenerator.IndexFromContainer(cell);
            if (index >= 0) return index;
        }

        return -1;
    }

    private static DataGridCell? GetCellFromDataGrid(DataGridRow row, int columnIndex, DataGrid dataGrid)
    {
        if (row == null || columnIndex < 0 || columnIndex >= dataGrid.Columns.Count)
            return null;

        var cellsPresenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (cellsPresenter == null)
        {
            dataGrid.ScrollIntoView(row, dataGrid.Columns[columnIndex]);
            cellsPresenter = FindVisualChild<DataGridCellsPresenter>(row);
        }

        if (cellsPresenter == null) return null;
        return cellsPresenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
    }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is BoardCellValueDto cell)
        {
            return cell.Type switch
            {
                CellType.SerialPort => SerialPortTemplate,
                CellType.Status => StatusTemplate,
                CellType.ScanSn => ScanSnTemplate,
                _ => NormalTemplate
            };
        }
        return NormalTemplate;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindParent<T>(parentObject);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nestedChild = FindVisualChild<T>(child);
            if (nestedChild != null) return nestedChild;
        }
        return null;
    }
}
