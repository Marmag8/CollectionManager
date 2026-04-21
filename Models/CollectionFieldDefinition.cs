namespace CollectionManager.Models;

public class CollectionFieldDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;
    public List<string> ChoiceOptions { get; set; } = [];
}
