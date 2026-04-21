using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CollectionManager.Models;

public class CollectionItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private decimal _price;
    private CollectionStatus _status = CollectionStatus.New;
    private int _rating = 5;
    private string _comment = string.Empty;
    private string _imagePath = string.Empty;
    private string _customValuesDisplay = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public decimal Price
    {
        get => _price;
        set => SetProperty(ref _price, value);
    }

    public CollectionStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsSold));
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    public int Rating
    {
        get => _rating;
        set => SetProperty(ref _rating, value);
    }

    public string Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public Dictionary<string, string> CustomValues { get; set; } = [];

    public string CustomValuesDisplay
    {
        get => _customValuesDisplay;
        set => SetProperty(ref _customValuesDisplay, value);
    }

    public bool IsSold => Status == CollectionStatus.Sold;

    public string StatusDisplay => Status switch
    {
        CollectionStatus.New => "Nowy",
        CollectionStatus.Used => "Użyty",
        CollectionStatus.ForSale => "Na sprzedaż",
        CollectionStatus.Sold => "Sprzedany",
        CollectionStatus.WantToBuy => "Chcę kupić",
        CollectionStatus.WantToSell => "Chcę sprzedać",
        _ => Status.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
