using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace CollectionManager.Services;

public class Utils
{
    private const string FileName = "collections.txt";
    private const string DefaultCharizardImage = "charizard_default.jpg";

    public ObservableCollection<CollectionModel> Collections { get; } = [];

    private string DataDirectory => GetDataDirectoryPath();
    private string DataFilePath => Path.Combine(DataDirectory, FileName);

    public static string GetDataDirectoryPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = FileSystem.Current.AppDataDirectory;
        }

        return Path.Combine(basePath, "CollectionManager");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDirectory);

        if (!File.Exists(DataFilePath))
        {
            CreateDefaultCollections();
            await SaveAsync();
            return;
        }

        var loaded = await LoadCollectionsFromFileAsync(DataFilePath);

        if (loaded.Count == 0)
        {
            CreateDefaultCollections();
            await SaveAsync();
            return;
        }

        var updatedDefaults = false;
        Collections.Clear();
        foreach (var collection in loaded)
        {
            updatedDefaults |= ApplyDefaultImages(collection);
            SortItems(collection);
            Collections.Add(collection);
        }

        if (updatedDefaults)
        {
            await SaveAsync();
        }
    }

    public async Task SaveAsync()
    {
        Directory.CreateDirectory(DataDirectory);

        var lines = new List<string>();
        foreach (var collection in Collections)
        {
            lines.Add($"COLLECTION|{Escape(collection.Id)}|{Escape(collection.Name)}|{Escape(collection.Description)}");

            foreach (var field in collection.CustomFields)
            {
                var options = string.Join(';', field.ChoiceOptions.Select(Escape));
                lines.Add($"FIELD|{Escape(collection.Id)}|{Escape(field.Id)}|{Escape(field.DisplayName)}|{field.FieldType}|{options}");
            }

            foreach (var item in collection.Items)
            {
                lines.Add($"ITEM|{Escape(collection.Id)}|{Escape(item.Id)}|{Escape(item.Name)}|{item.Price}|{item.Status}|{item.Rating}|{Escape(item.Comment)}|{Escape(item.ImagePath)}");
                foreach (var custom in item.CustomValues)
                {
                    lines.Add($"ITEMFIELD|{Escape(collection.Id)}|{Escape(item.Id)}|{Escape(custom.Key)}|{Escape(custom.Value)}");
                }
            }
        }

        await File.WriteAllLinesAsync(DataFilePath, lines);
    }

    public async Task<string> ExportCollectionAsync(CollectionModel collection)
    {
        Directory.CreateDirectory(DataDirectory);
        var exportPath = Path.Combine(DataDirectory, $"export_{SanitizeFileName(collection.Name)}_{DateTime.Now:yyyyMMddHHmmss}.txt");

        var temp = new ObservableCollection<CollectionModel> { CloneCollection(collection) };
        var lines = Serialize(temp);
        await File.WriteAllLinesAsync(exportPath, lines);
        return exportPath;
    }

    public async Task<(int AddedCount, int DuplicateNameCount)> ImportAndMergeCollectionAsync(string importFilePath)
    {
        if (!File.Exists(importFilePath))
        {
            return (0, 0);
        }

        var incoming = await LoadCollectionsFromFileAsync(importFilePath);
        if (incoming.Count == 0)
        {
            return (0, 0);
        }

        var addedCount = 0;
        var duplicateNameCount = 0;

        foreach (var incomingCollection in incoming)
        {
            var existing = Collections.FirstOrDefault(c =>
                c.Id == incomingCollection.Id ||
                string.Equals(c.Name, incomingCollection.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                SortItems(incomingCollection);
                Collections.Add(incomingCollection);
                addedCount += incomingCollection.Items.Count;
                continue;
            }

            foreach (var field in incomingCollection.CustomFields)
            {
                if (!existing.CustomFields.Any(f => f.Id == field.Id || string.Equals(f.DisplayName, field.DisplayName, StringComparison.OrdinalIgnoreCase)))
                {
                    existing.CustomFields.Add(field);
                }
            }

            foreach (var incomingItem in incomingCollection.Items)
            {
                var hasSameName = existing.Items.Any(i =>
                    string.Equals(i.Name, incomingItem.Name, StringComparison.OrdinalIgnoreCase));

                if (hasSameName)
                {
                    duplicateNameCount++;
                }

                var toAdd = new CollectionItem
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Name = incomingItem.Name,
                    Price = incomingItem.Price,
                    Status = incomingItem.Status,
                    Rating = incomingItem.Rating,
                    Comment = incomingItem.Comment,
                    ImagePath = incomingItem.ImagePath,
                    CustomValues = new Dictionary<string, string>(incomingItem.CustomValues)
                };

                existing.Items.Add(toAdd);
                addedCount++;
            }

            SortItems(existing);
        }

        await SaveAsync();
        return (addedCount, duplicateNameCount);
    }

    public void SortItems(CollectionModel collection)
    {
        var ordered = collection.Items
            .OrderBy(i => i.IsSold)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        collection.Items.Clear();
        foreach (var item in ordered)
        {
            collection.Items.Add(item);
        }
    }

    public static string GetDefaultCollectionsFilePath() => Path.Combine(GetDataDirectoryPath(), FileName);

    private static List<string> Serialize(ObservableCollection<CollectionModel> collections)
    {
        var lines = new List<string>();

        foreach (var collection in collections)
        {
            lines.Add($"COLLECTION|{Escape(collection.Id)}|{Escape(collection.Name)}|{Escape(collection.Description)}");
            foreach (var field in collection.CustomFields)
            {
                var options = string.Join(';', field.ChoiceOptions.Select(Escape));
                lines.Add($"FIELD|{Escape(collection.Id)}|{Escape(field.Id)}|{Escape(field.DisplayName)}|{field.FieldType}|{options}");
            }

            foreach (var item in collection.Items)
            {
                lines.Add($"ITEM|{Escape(collection.Id)}|{Escape(item.Id)}|{Escape(item.Name)}|{item.Price}|{item.Status}|{item.Rating}|{Escape(item.Comment)}|{Escape(item.ImagePath)}");
                foreach (var custom in item.CustomValues)
                {
                    lines.Add($"ITEMFIELD|{Escape(collection.Id)}|{Escape(item.Id)}|{Escape(custom.Key)}|{Escape(custom.Value)}");
                }
            }
        }

        return lines;
    }

    private static async Task<ObservableCollection<CollectionModel>> LoadCollectionsFromFileAsync(string filePath)
    {
        var result = new ObservableCollection<CollectionModel>();
        var collectionsById = new Dictionary<string, CollectionModel>();
        var itemsByIds = new Dictionary<(string CollectionId, string ItemId), CollectionItem>();

        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parts = SplitAndUnescape(rawLine, '|');
            if (parts.Length < 2)
            {
                continue;
            }

            switch (parts[0])
            {
                case "COLLECTION" when parts.Length >= 4:
                    {
                        var collection = new CollectionModel
                        {
                            Id = parts[1],
                            Name = NormalizeLoadedValue(parts[2]),
                            Description = NormalizeLoadedValue(parts[3])
                        };

                        collectionsById[collection.Id] = collection;
                        result.Add(collection);
                        break;
                    }
                case "FIELD" when parts.Length >= 6:
                    {
                        if (!collectionsById.TryGetValue(parts[1], out var collection))
                        {
                            break;
                        }

                        if (!Enum.TryParse(parts[4], out CustomFieldType fieldType))
                        {
                            fieldType = CustomFieldType.Text;
                        }

                        var field = new CollectionFieldDefinition
                        {
                            Id = parts[2],
                            DisplayName = NormalizeLoadedValue(parts[3]),
                            FieldType = fieldType,
                            ChoiceOptions = SplitAndUnescape(parts[5], ';').Select(NormalizeLoadedValue).Where(o => !string.IsNullOrWhiteSpace(o)).ToList()
                        };

                        collection.CustomFields.Add(field);
                        break;
                    }
                case "ITEM" when parts.Length >= 9:
                    {
                        if (!collectionsById.TryGetValue(parts[1], out var collection))
                        {
                            break;
                        }

                        _ = decimal.TryParse(parts[4], out var price);
                        _ = Enum.TryParse(parts[5], out CollectionStatus status);
                        _ = int.TryParse(parts[6], out var rating);

                        var item = new CollectionItem
                        {
                            Id = parts[2],
                            Name = NormalizeLoadedValue(parts[3]),
                            Price = price,
                            Status = status,
                            Rating = Math.Clamp(rating, 1, 10),
                            Comment = NormalizeLoadedValue(parts[7]),
                            ImagePath = NormalizeLoadedValue(parts[8])
                        };

                        collection.Items.Add(item);
                        itemsByIds[(parts[1], parts[2])] = item;
                        break;
                    }
                case "ITEMFIELD" when parts.Length >= 5:
                    {
                        if (itemsByIds.TryGetValue((parts[1], parts[2]), out var item))
                        {
                            item.CustomValues[NormalizeLoadedValue(parts[3])] = NormalizeLoadedValue(parts[4]);
                        }

                        break;
                    }
            }
        }

        foreach (var collection in result)
        {
            var validFields = collection.CustomFields.Select(f => f.Id).ToHashSet();
            foreach (var item in collection.Items)
            {
                foreach (var key in item.CustomValues.Keys.Where(k => !validFields.Contains(k)).ToList())
                {
                    item.CustomValues.Remove(key);
                }
            }
        }

        return result;
    }

    private static CollectionModel CloneCollection(CollectionModel source)
    {
        var clone = new CollectionModel
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description
        };

        foreach (var field in source.CustomFields)
        {
            clone.CustomFields.Add(new CollectionFieldDefinition
            {
                Id = field.Id,
                DisplayName = field.DisplayName,
                FieldType = field.FieldType,
                ChoiceOptions = [.. field.ChoiceOptions]
            });
        }

        foreach (var item in source.Items)
        {
            clone.Items.Add(new CollectionItem
            {
                Id = item.Id,
                Name = item.Name,
                Price = item.Price,
                Status = item.Status,
                Rating = item.Rating,
                Comment = item.Comment,
                ImagePath = item.ImagePath,
                CustomValues = new Dictionary<string, string>(item.CustomValues)
            });
        }

        return clone;
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        return string.IsNullOrWhiteSpace(builder.ToString()) ? "collection" : builder.ToString();
    }

    private static string Escape(string value)
    {
        var raw = value ?? string.Empty;
        return raw
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string[] SplitAndUnescape(string input, char separator)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [];
        }

        var items = new List<string>();
        var builder = new StringBuilder();
        var escaping = false;

        foreach (var c in input)
        {
            if (escaping)
            {
                builder.Append(c switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    _ => c
                });
                escaping = false;
                continue;
            }

            if (c == '\\')
            {
                escaping = true;
                continue;
            }

            if (c == separator)
            {
                items.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(c);
        }

        if (escaping)
        {
            builder.Append('\\');
        }

        items.Add(builder.ToString());
        return [.. items];
    }

    private static string NormalizeLoadedValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TryDecodeLegacyBase64(value, out var decoded))
        {
            return decoded;
        }

        return value;
    }

    private static bool TryDecodeLegacyBase64(string value, out string decoded)
    {
        decoded = string.Empty;

        if (!value.Contains('=') && !value.Contains('+') && !value.Contains('/'))
        {
            return false;
        }

        if (value.Length % 4 != 0)
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            decoded = Encoding.UTF8.GetString(bytes);

            foreach (var c in decoded)
            {
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    decoded = string.Empty;
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void CreateDefaultCollections()
    {
        Collections.Clear();

        var cardsCollection = new CollectionModel
        {
            Name = "Karty",
            Description = "Kolekcja kart TCG"
        };
        var cardsField = new CollectionFieldDefinition
        {
            DisplayName = "Stan karty",
            FieldType = CustomFieldType.Choice,
            ChoiceOptions = ["Mint", "Near Mint", "Played"]
        };
        cardsCollection.CustomFields.Add(cardsField);
        cardsCollection.Items.Add(new CollectionItem
        {
            Name = "Charizard",
            Price = 1299,
            Status = CollectionStatus.New,
            Rating = 10,
            Comment = "Ulubiona karta",
            ImagePath = DefaultCharizardImage,
            CustomValues = new Dictionary<string, string> { [cardsField.Id] = "Mint" }
        });

        cardsCollection.Items.Add(new CollectionItem
        {
            Name = "Blue-Eyes White Dragon",
            Price = 399,
            Status = CollectionStatus.Used,
            Rating = 8,
            Comment = "W dobrym stanie",
            CustomValues = new Dictionary<string, string> { [cardsField.Id] = "Near Mint" }
        });

        var figurinesCollection = new CollectionModel
        {
            Name = "Figurki",
            Description = "Kolekcja figurek"
        };
        var figurinesField = new CollectionFieldDefinition
        {
            DisplayName = "Wysokość (cm)",
            FieldType = CustomFieldType.Number
        };
        figurinesCollection.CustomFields.Add(figurinesField);
        figurinesCollection.Items.Add(new CollectionItem
        {
            Name = "Batman Premium",
            Price = 249,
            Status = CollectionStatus.New,
            Rating = 9,
            Comment = "Limitowana edycja",
            CustomValues = new Dictionary<string, string> { [figurinesField.Id] = "32" }
        });
        figurinesCollection.Items.Add(new CollectionItem
        {
            Name = "Geralt",
            Price = 179,
            Status = CollectionStatus.ForSale,
            Rating = 7,
            Comment = "Bez pudełka",
            CustomValues = new Dictionary<string, string> { [figurinesField.Id] = "24" }
        });

        var carsCollection = new CollectionModel
        {
            Name = "Auta",
            Description = "Modele samochodów"
        };
        var carsField = new CollectionFieldDefinition
        {
            DisplayName = "Marka",
            FieldType = CustomFieldType.Text
        };
        carsCollection.CustomFields.Add(carsField);
        carsCollection.Items.Add(new CollectionItem
        {
            Name = "Model 911 1:18",
            Price = 320,
            Status = CollectionStatus.New,
            Rating = 9,
            Comment = "Doskonały stan",
            CustomValues = new Dictionary<string, string> { [carsField.Id] = "Porsche" }
        });
        carsCollection.Items.Add(new CollectionItem
        {
            Name = "Mustang 1967 1:24",
            Price = 159,
            Status = CollectionStatus.WantToSell,
            Rating = 8,
            Comment = "Gotowy do sprzedaży",
            CustomValues = new Dictionary<string, string> { [carsField.Id] = "Ford" }
        });

        SortItems(cardsCollection);
        SortItems(figurinesCollection);
        SortItems(carsCollection);

        Collections.Add(cardsCollection);
        Collections.Add(figurinesCollection);
        Collections.Add(carsCollection);
    }

    private static bool ApplyDefaultImages(CollectionModel collection)
    {
        var changed = false;

        foreach (var item in collection.Items)
        {
            var isCharizard = string.Equals(item.Name, "Charizard", StringComparison.OrdinalIgnoreCase);
            var hasLegacySvg = string.Equals(item.ImagePath, "charizard_default.svg", StringComparison.OrdinalIgnoreCase);
            var hasLegacyPng = string.Equals(item.ImagePath, "charizard_default.png", StringComparison.OrdinalIgnoreCase);
            var isMissing = string.IsNullOrWhiteSpace(item.ImagePath);

            if (isCharizard && (isMissing || hasLegacySvg || hasLegacyPng))
            {
                item.ImagePath = DefaultCharizardImage;
                changed = true;
            }
        }

        return changed;
    }
}
