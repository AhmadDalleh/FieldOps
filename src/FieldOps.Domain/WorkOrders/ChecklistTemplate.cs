using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

/// <summary>A list of tasks copied onto new work orders of <see cref="WorkOrderType"/>.</summary>
public sealed class ChecklistTemplate : AuditableEntity
{
    private readonly List<ChecklistTemplateItem> _items = [];

    private ChecklistTemplate() { }

    public string Name { get; private set; } = null!;
    /// <summary>The work order type the template applies to; null for a template that is not applied automatically.</summary>
    public WorkOrderType? WorkOrderType { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<ChecklistTemplateItem> Items => _items;

    public static ChecklistTemplate Create(string name, WorkOrderType? type, IEnumerable<string> items)
    {
        var template = new ChecklistTemplate { Name = name, WorkOrderType = type };
        template._items.AddRange(items.Select((description, i) => new ChecklistTemplateItem(template.Id, i + 1, description)));
        return template;
    }

    public void Deactivate() => IsActive = false;
}

public sealed class ChecklistTemplateItem : Entity
{
    private ChecklistTemplateItem() { }

    internal ChecklistTemplateItem(Guid templateId, int sortOrder, string description)
    {
        TemplateId = templateId;
        SortOrder = sortOrder;
        Description = description;
    }

    public Guid TemplateId { get; private init; }
    public int SortOrder { get; private init; }
    public string Description { get; private init; } = null!;
}
