namespace CollectionManager.Views;

public partial class ItemEditPage : ContentPage
{
    private readonly CollectionModel _collection;
    private readonly CollectionItem _item;
    private readonly Utils _utils;
    private readonly bool _isNew;
    private bool _isAdjustingRating;

    private readonly Dictionary<string, View> _dynamicInputs = [];

    public ItemEditPage(CollectionModel collection, CollectionItem item, Utils utils, bool isNew)
    {
        InitializeComponent();
        _collection = collection;
        _item = item;
        _utils = utils;
        _isNew = isNew;

        HeaderLabel.Text = isNew ? "Nowy element" : "Edycja elementu";

        StatusPicker.ItemsSource = Enum.GetValues<CollectionStatus>().Select(ToPolish).ToList();

        NameEntry.Text = item.Name;
        PriceEntry.Text = item.Price.ToString();
        StatusPicker.SelectedItem = ToPolish(item.Status);
        RatingStepper.Value = Math.Clamp(item.Rating, 1, 10);
        RatingLabel.Text = $"Ocena: {(int)RatingStepper.Value}/10";
        CommentEditor.Text = item.Comment;
        ImagePathEntry.Text = item.ImagePath;
        PreviewImage.Source = item.ImagePath;

        BuildCustomFieldInputs();
    }

    private void BuildCustomFieldInputs()
    {
        CustomFieldsContainer.Children.Clear();
        _dynamicInputs.Clear();

        foreach (var field in _collection.CustomFields)
        {
            var label = new Label { Text = field.DisplayName, TextColor = Colors.White };
            CustomFieldsContainer.Children.Add(label);

            _item.CustomValues.TryGetValue(field.Id, out var currentValue);
            currentValue ??= string.Empty;

            View input;
            switch (field.FieldType)
            {
                case CustomFieldType.Number:
                    input = new Entry { Keyboard = Keyboard.Numeric, Text = currentValue, Placeholder = "Wartość liczbowa" };
                    break;
                case CustomFieldType.Choice:
                    var picker = new Picker { ItemsSource = field.ChoiceOptions };
                    picker.SelectedItem = field.ChoiceOptions.FirstOrDefault(o => string.Equals(o, currentValue, StringComparison.OrdinalIgnoreCase));
                    input = picker;
                    break;
                default:
                    input = new Entry { Text = currentValue, Placeholder = "Wartość tekstowa" };
                    break;
            }

            _dynamicInputs[field.Id] = input;
            CustomFieldsContainer.Children.Add(input);
        }
    }

    private void OnRatingChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isAdjustingRating)
        {
            return;
        }

        var clamped = Math.Clamp((int)Math.Round(e.NewValue), 1, 10);

        if (Math.Abs(RatingStepper.Value - clamped) > double.Epsilon)
        {
            _isAdjustingRating = true;
            RatingStepper.Value = clamped;
            _isAdjustingRating = false;
        }

        RatingLabel.Text = $"Ocena: {clamped}/10";
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Wybierz grafikę elementu"
            });

            if (result is null)
            {
                return;
            }

            ImagePathEntry.Text = result.FullPath;
            PreviewImage.Source = result.FullPath;
        }
        catch
        {
            await DisplayAlert("Błąd", "Nie udało się wybrać grafiki.", "OK");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("Brak nazwy", "Nazwa elementu jest wymagana.", "OK");
            return;
        }

        if (!decimal.TryParse(PriceEntry.Text, out var price))
        {
            price = 0m;
        }

        var status = ParseStatus(StatusPicker.SelectedItem?.ToString());

        var possibleDuplicate = _collection.Items.FirstOrDefault(i =>
            !ReferenceEquals(i, _item) &&
            string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

        if (possibleDuplicate is not null)
        {
            var shouldContinue = await DisplayAlert("Duplikat", "Element o tej nazwie już istnieje. Dodać mimo to?", "Tak", "Nie");
            if (!shouldContinue)
            {
                return;
            }
        }

        _item.Name = name;
        _item.Price = price;
        _item.Status = status;
        _item.Rating = Math.Clamp((int)RatingStepper.Value, 1, 10);
        _item.Comment = CommentEditor.Text?.Trim() ?? string.Empty;
        _item.ImagePath = ImagePathEntry.Text?.Trim() ?? string.Empty;

        foreach (var field in _collection.CustomFields)
        {
            if (!_dynamicInputs.TryGetValue(field.Id, out var input))
            {
                continue;
            }

            var value = input switch
            {
                Entry entry => entry.Text?.Trim() ?? string.Empty,
                Picker picker => picker.SelectedItem?.ToString() ?? string.Empty,
                _ => string.Empty
            };

            _item.CustomValues[field.Id] = value;
        }

        if (_isNew && !_collection.Items.Contains(_item))
        {
            _collection.Items.Add(_item);
        }

        _utils.SortItems(_collection);
        await _utils.SaveAsync();
        await Navigation.PopAsync();
    }

    private static string ToPolish(CollectionStatus status) => status switch
    {
        CollectionStatus.New => "Nowy",
        CollectionStatus.Used => "Użyty",
        CollectionStatus.ForSale => "Na sprzedaż",
        CollectionStatus.Sold => "Sprzedany",
        CollectionStatus.WantToBuy => "Chcę kupić",
        CollectionStatus.WantToSell => "Chcę sprzedać",
        _ => status.ToString()
    };

    private static CollectionStatus ParseStatus(string? status) => status switch
    {
        "Nowy" => CollectionStatus.New,
        "Użyty" => CollectionStatus.Used,
        "Na sprzedaż" => CollectionStatus.ForSale,
        "Sprzedany" => CollectionStatus.Sold,
        "Chcę kupić" => CollectionStatus.WantToBuy,
        "Chcę sprzedać" => CollectionStatus.WantToSell,
        _ => CollectionStatus.New
    };
}
