using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace StyloIssues.UI.TagHelpers;

[HtmlTargetElement("sb-feedback-detail", TagStructure = TagStructure.WithoutEndTag)]
public sealed class SbFeedbackDetailTagHelper : TagHelper
{
    private readonly IViewComponentHelper _vc;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    [HtmlAttributeName("number")]
    public int Number { get; set; }

    public SbFeedbackDetailTagHelper(IViewComponentHelper vc)
    {
        _vc = vc;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ((IViewContextAware)_vc).Contextualize(ViewContext);
        output.TagName = null;
        output.Content.SetHtmlContent(await _vc.InvokeAsync("FeedbackDetail", new { number = Number }));
    }
}
