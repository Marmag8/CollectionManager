namespace CollectionManager.Views;

public partial class CollectionsPage : ContentPage
{
    private readonly Utils _utils;

    public CollectionsPage(Utils utils)
    {
        InitializeComponent();
        _utils = utils;
        CollectionsView.ItemsSource = _utils.Collections;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_utils.Collections.Count == 0)
        {
            await _utils.InitializeAsync();
        }

#if DEBUG
        DebugPathLabel.IsVisible = true;
        DebugPathLabel.Text = $"Debug path: {Utils.GetDefaultCollectionsFilePath()}";
#endif
    }

    private async void OnAddCollectionClicked(object? sender, EventArgs e)
    {
        var name = CollectionNameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Brak danych", "Podaj nazwę kolekcji.", "OK");
            return;
        }

        var description = await DisplayPromptAsync("Opis kolekcji", "Podaj krótki opis tej kolekcji:", "OK", "Pomiń");

        _utils.Collections.Add(new CollectionModel
        {
            Name = name,
            Description = description?.Trim() ?? string.Empty
        });

        CollectionNameEntry.Text = string.Empty;
        await _utils.SaveAsync();
    }

    private async void OnDeleteCollectionClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CollectionModel collection })
        {
            return;
        }

        var confirm = await DisplayAlert("Usuwanie", $"Usunąć kolekcję '{collection.Name}'?", "Tak", "Nie");
        if (!confirm)
        {
            return;
        }

        _utils.Collections.Remove(collection);
        await _utils.SaveAsync();
    }

    private async void OnCollectionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not CollectionModel selected)
        {
            return;
        }

        CollectionsView.SelectedItem = null;
        await Navigation.PushAsync(new CollectionDetailsPage(_utils, selected));
    }
}
