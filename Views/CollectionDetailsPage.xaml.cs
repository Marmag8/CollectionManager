namespace CollectionManager.Views;

public partial class CollectionDetailsPage : ContentPage
{
    private readonly Utils _utils;
    private readonly CollectionModel _collection;

    public CollectionDetailsPage(Utils utils, CollectionModel collection)
    {
        InitializeComponent();
        _utils = utils;
        _collection = collection;

        CollectionTitleLabel.Text = collection.Name;
        ItemsCollectionView.ItemsSource = collection.Items;
        RefreshColumnsLabel();
        RefreshCustomValuesDisplay();
        RefreshSummary();
    }

    private void RefreshColumnsLabel()
    {
        var customColumns = _collection.CustomFields.Select(f => f.DisplayName).ToList();
        var baseColumns = new List<string> { "Nazwa", "Cena", "Status", "Ocena", "Komentarz", "Grafika" };
        baseColumns.AddRange(customColumns);
        ColumnsLabel.Text = $"Właściwości: {string.Join(", ", baseColumns)}";
    }

    private async void OnAddItemClicked(object? sender, EventArgs e)
    {
        var newItem = new CollectionItem();
        var page = new ItemEditPage(_collection, newItem, _utils, isNew: true);
        await Navigation.PushAsync(page);
    }

    private async void OnAddFieldClicked(object? sender, EventArgs e)
    {
        var displayName = await DisplayPromptAsync("Nowa właściwość", "Podaj nazwę właściwości:", "Dalej", "Anuluj");
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var fieldTypePick = await DisplayActionSheet("Wybierz typ właściwości", "Anuluj", null, "Tekst", "Liczba", "Wybór z listy");
        if (fieldTypePick == "Anuluj" || string.IsNullOrWhiteSpace(fieldTypePick))
        {
            return;
        }

        var fieldType = fieldTypePick switch
        {
            "Liczba" => CustomFieldType.Number,
            "Wybór z listy" => CustomFieldType.Choice,
            _ => CustomFieldType.Text
        };

        var field = new CollectionFieldDefinition
        {
            DisplayName = displayName.Trim(),
            FieldType = fieldType
        };

        if (fieldType == CustomFieldType.Choice)
        {
            var options = await DisplayPromptAsync("Opcje", "Podaj opcje oddzielone przecinkami:", "OK", "Anuluj", placeholder: "Nowa,Używana,Unikat");
            if (string.IsNullOrWhiteSpace(options))
            {
                return;
            }

            field.ChoiceOptions = options.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        _collection.CustomFields.Add(field);
        foreach (var item in _collection.Items)
        {
            item.CustomValues[field.Id] = string.Empty;
        }

        RefreshColumnsLabel();
        RefreshCustomValuesDisplay();
        await _utils.SaveAsync();
    }

    private async void OnImportExportClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Eksport / Import", "Anuluj", null, "Eksportuj kolekcję", "Importuj i scal");
        switch (action)
        {
            case "Eksportuj kolekcję":
                {
                    var path = await _utils.ExportCollectionAsync(_collection);
                    await DisplayAlert("Eksport zakończony", $"Plik zapisano:\n{path}", "OK");
                    break;
                }
            case "Importuj i scal":
                {
                    var defaultPath = Utils.GetDataDirectoryPath();
                    var path = await DisplayPromptAsync("Import", "Podaj pełną ścieżkę do pliku importu:", "Importuj", "Anuluj", initialValue: defaultPath + Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        return;
                    }

                    var importResult = await _utils.ImportAndMergeCollectionAsync(path.Trim());
                    var message = $"Dodano nowych rekordów: {importResult.AddedCount}";
                    if (importResult.DuplicateNameCount > 0)
                    {
                        message += $"\nUwaga: zaimportowano {importResult.DuplicateNameCount} elementów o nazwach już istniejących.";
                    }

                    await DisplayAlert("Import", message, "OK");
                    RefreshCustomValuesDisplay();
                    RefreshSummary();
                    break;
                }
        }
    }

    private async void OnEditItemClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CollectionItem item })
        {
            return;
        }

        await Navigation.PushAsync(new ItemEditPage(_collection, item, _utils, isNew: false));
    }

    private async void OnDeleteItemClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: CollectionItem item })
        {
            return;
        }

        var confirm = await DisplayAlert("Usuwanie", $"Usunąć element '{item.Name}'?", "Tak", "Nie");
        if (!confirm)
        {
            return;
        }

        _collection.Items.Remove(item);
        _utils.SortItems(_collection);
        RefreshCustomValuesDisplay();
        RefreshSummary();
        await _utils.SaveAsync();
    }

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not CollectionItem item)
        {
            return;
        }

        ItemsCollectionView.SelectedItem = null;
        await Navigation.PushAsync(new ItemEditPage(_collection, item, _utils, isNew: false));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _utils.SortItems(_collection);
        RefreshColumnsLabel();
        RefreshCustomValuesDisplay();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        TotalCountLabel.Text = $"Liczba przedmiotów: {_collection.Items.Count}";
        SoldCountLabel.Text = $"Przedmioty sprzedane: {_collection.Items.Count(i => i.Status == CollectionStatus.Sold)}";
        WantToSellCountLabel.Text = $"Przedmioty do sprzedaży: {_collection.Items.Count(i => i.Status == CollectionStatus.WantToSell || i.Status == CollectionStatus.ForSale)}";
    }

    private void RefreshCustomValuesDisplay()
    {
        foreach (var item in _collection.Items)
        {
            var values = _collection.CustomFields
                .Select(field =>
                {
                    item.CustomValues.TryGetValue(field.Id, out var value);
                    value ??= string.Empty;
                    return string.IsNullOrWhiteSpace(value) ? null : $"{field.DisplayName}: {value}";
                })
                .Where(v => !string.IsNullOrWhiteSpace(v));

            item.CustomValuesDisplay = string.Join(Environment.NewLine, values!);
        }
    }
}
