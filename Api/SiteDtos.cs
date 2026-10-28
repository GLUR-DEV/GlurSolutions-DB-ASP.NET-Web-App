namespace GlurSolutions.Web.Api;

// The shape of the JSON that SiteDataBuilder produces and SnapshotService uploads to Blob storage.
// The website reads those files; there is no live API any more.

public record TechnologyDto(string Key, string Label);

public record ProjectCardDto(
    string Slug, string Title, string ShortDescription,
    string? CoverImagePath, string? CoverAlt,
    IReadOnlyList<TechnologyDto> Technologies);

public record SlideDto(string StageLabel, string Title, string Caption, string? ImagePath, string? ImageAlt);

public record SlideshowDto(string Kind, string Heading, IReadOnlyList<SlideDto> Slides);

public record NoteDto(string Kind, string Heading, string Body);

public record ProjectDetailDto(
    string Slug, string Title, string ShortDescription, string? Summary,
    string? CoverImagePath, string? CoverAlt,
    string? Role, string? Tools, string? Timeframe, DateTime UpdatedUtc,
    IReadOnlyList<TechnologyDto> Technologies,
    IReadOnlyList<SlideshowDto> Slideshows,
    IReadOnlyList<NoteDto> Notes);
