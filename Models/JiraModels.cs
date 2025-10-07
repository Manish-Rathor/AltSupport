using System.Text.Json.Serialization;
using Alt_Support.Converters;

namespace Alt_Support.Models
{
    public class JiraWebhookRequest
    {
        public string WebhookEvent { get; set; } = string.Empty;
        public TicketWebhookData Issue { get; set; } = new TicketWebhookData();
        public UserInfo User { get; set; } = new UserInfo();
        public ChangelogInfo? Changelog { get; set; }
    }

    public class TicketWebhookData
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public TicketFields Fields { get; set; } = new TicketFields();
    }

    public class TicketFields
    {
        public string Summary { get; set; } = string.Empty;
        public DescriptionField? Description { get; set; }
        public IssueTypeInfo? Issuetype { get; set; }
        public StatusInfo? Status { get; set; }
        public PriorityInfo? Priority { get; set; }
        public UserInfo? Assignee { get; set; }
        public UserInfo? Reporter { get; set; }
        public ProjectInfo? Project { get; set; }
        public List<string>? Labels { get; set; }
        public List<ComponentInfo>? Components { get; set; }
        [JsonConverter(typeof(JiraDateTimeConverter))]
        public DateTime Created { get; set; }
        [JsonConverter(typeof(JiraDateTimeConverter))]
        public DateTime Updated { get; set; }
        [JsonConverter(typeof(JiraNullableDateTimeConverter))]
        public DateTime? Resolutiondate { get; set; }
        public ResolutionInfo? Resolution { get; set; }
        public CustomField10144? PRLinksField { get; set; }
    }

    public class DescriptionField
    {
        public string Type { get; set; } = string.Empty;
        public int Version { get; set; }
        public List<ContentItem>? Content { get; set; }
    }

    public class ContentItem
    {
        public string Type { get; set; } = string.Empty;
        public List<ContentItem>? Content { get; set; }
        public string? Text { get; set; }
        public List<Mark>? Marks { get; set; }
    }

    public class Mark
    {
        public string Type { get; set; } = string.Empty;
        public MarkAttrs? Attrs { get; set; }
    }

    public class MarkAttrs
    {
        public string? Href { get; set; }
    }

    public class IssueTypeInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class StatusInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class PriorityInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class UserInfo
    {
        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
    }

    public class ProjectInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }



    public class ComponentInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class ResolutionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CustomField10144
    {
        public string Type { get; set; } = string.Empty;
        public int Version { get; set; }
        public List<ContentItem>? Content { get; set; }
    }

    public class ChangelogInfo
    {
        public string Id { get; set; } = string.Empty;
        public List<ChangeItem>? Items { get; set; }
    }

    public class ChangeItem
    {
        public string Field { get; set; } = string.Empty;
        public string? From { get; set; }
        public string? FromString { get; set; }
        public string? To { get; set; }
        public new string? ToString { get; set; }
    }
}