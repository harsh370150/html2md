﻿using FluentAssertions;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Html2md.Tests.Unit
{
    public class MarkdownConverterTests
    {
        private static readonly Uri pageUrl = new Uri("https://converttest.goatly.net/page/name");
        private static readonly Uri absoluteImageUrlSameDomain = new Uri("https://converttest.goatly.net/images/img.png");
        private static readonly Uri absoluteImageUrlDifferentDomain = new Uri("https://other.goatly.net/images/img.png");
        private static readonly Uri relativeImageUrl = new Uri("../img.png", UriKind.Relative);
        private static readonly Uri imageUrl = new Uri("/static/images/img.png", UriKind.Relative);
        private static readonly Uri missingImageUrl = new Uri("/static/images/missing.png", UriKind.Relative);
        private static readonly byte[] absoluteImageUrlSameDomainData = new byte[] { 1 };
        private static readonly byte[] absoluteImageUrlDifferentDomainData = new byte[] { 2 };
        private static readonly byte[] relativeImageUrlData = new byte[] { 3 };
        private static readonly byte[] imageUrlData = new byte[] { 4 };

        [Fact]
        public async Task ShouldConvertMultipleDocumentsInOneGoAndDownloadUniqueImagesOnce()
        {
            await TestConverter(
                new[]
                {
                    "",
                    $@"<div>An image: <img src=""{imageUrl}"" alt=""Title"" > </div>",
                    $@"<div>Another image: <img src=""{relativeImageUrl}"" alt=""Title"" > </div>",
                    $@"<div>A repeated image: <img src=""{relativeImageUrl}"" alt=""Title"" > </div>"
                },
                new[] 
                {
                    "",
                    "An image: ![Title](img.png)",
                    "Another image: ![Title](img.png)",
                    "A repeated image: ![Title](img.png)",
                },
                expectedImages: new[]
                {
                    new ReferencedImage(new Uri(pageUrl, imageUrl), imageUrlData),
                    new ReferencedImage(new Uri(pageUrl, relativeImageUrl), relativeImageUrlData),
                });
        }

        [Fact]
        public async Task ShouldConvertEmptyPageToEmptyMarkdown()
        {
            await TestConverter(
                "",
                "");
        }

        [Fact]
        public async Task ShouldApplyFrontMatterWhenConfigured()
        {
            await TestConverter(
                "<body><h1>Doc title</h1><p>test</p></body>",
                @"---
Title: ""Doc title""
---
test

",
                new ConversionOptions
                {
                    ExcludeTags = { "h1" },
                    FrontMatter =
                    {
                        Enabled = true,
                        SingleValueProperties = { { "Title", new PropertyMatchExpression("/body/h1") } }
                    }
                });
        }

        [Fact]
        public async Task ShouldConvertFrontMatterPropertyToData()
        {
            await TestConverter(
                "<body><h1>Thursday, August 7, 2014 11:55:08 AM</h1><p>test</p></body>",
                @"---
Title: ""2014-08-07T11:55:08.0000000""
---
test

",
                new ConversionOptions
                {
                    ExcludeTags = { "h1" },
                    FrontMatter =
                    {
                        Enabled = true,
                        SingleValueProperties = { { "Title", new PropertyMatchExpression("/body/h1", PropertyDataType.Date) } }
                    }
                });
        }

        [Fact]
        public async Task ShouldDecodeHtmlInMarkdownText()
        {
            await TestConverter(
                "<em>test&apos;s</em>",
                "*test's*");
        }

        [Fact]
        public async Task ShouldConvertEm()
        {
            await TestConverter(
                "<em>test</em>",
                "*test*");
        }

        [Fact]
        public async Task ShouldConvertItalic()
        {
            await TestConverter(
                "<i>test</i>",
                "*test*");
        }

        [Fact]
        public async Task ShouldOnlyProcessIncludedTags()
        {
            await TestConverter(
                "<body><aside><i>Should be ignored</i></aside><article><em>Should appear</em></article></body>",
                "*Should appear*",
                options: new ConversionOptions
                {
                    IncludeTags = { "article" }
                });
        }

        [Fact]
        public async Task ShouldOnlyProcessIncludedNodesFromXPath()
        {
            await TestConverter(
                "<body><article>Should appear</article><article>Should also appear</article><p>This too</p></body>",
                @"Should appear

Should also appear

This too

",
                options: new ConversionOptions
                {
                    IncludeTags = { "//article", "//p" }
                });
        }

        [Fact]
        public async Task ShouldExcludeSpecificNodesIndicatedByXPaths()
        {
            await TestConverter(
                "<body><article>Should appear <div class='comments'>Should be ignored</div></article></body>",
                @"Should appear ",
                options: new ConversionOptions
                {
                    IncludeTags = { "//article" },
                    ExcludeTags = { "//div[@class='comments']"}
                });
        }

        [Fact]
        public async Task ShouldNotProcessExcludedTags()
        {
            await TestConverter(
                "<body><aside><i>Should be ignored</i></aside><article><em>Should appear</em></article></body>",
                "*Should appear*",
                options: new ConversionOptions
                {
                    ExcludeTags = { "aside" }
                });
        }

        [Fact]
        public async Task ShouldNotProcessExcludedTagsEvenIfContainedInIncludedTags()
        {
            await TestConverter(
                "<body><aside><i>Should be ignored</i></aside><article><em>Should appear</em><aside>IGNORED</aside></article></body>",
                "*Should appear*",
                options: new ConversionOptions
                {
                    IncludeTags = { "article" },
                    ExcludeTags = { "aside" }
                });
        }

        [Fact]
        public async Task ShouldEscapeMarkdownCommands()
        {
            await TestConverter(
                "<strong>\\`*_{}[]()#+-.!</strong>",
                "**\\\\\\`\\*\\_\\{\\}\\[\\]\\(\\)\\#\\+\\-\\.\\!**");
        }

        [Fact]
        public async Task ShouldConvertStrong()
        {
            await TestConverter(
                "<strong>test</strong>",
                "**test**");
        }

        [Fact]
        public async Task ShouldConvertBold()
        {
            await TestConverter(
                "<b>test</b>",
                "**test**");
        }

        [Fact]
        public async Task ShouldConvertSimpleUnorderedList()
        {
            await TestConverter(
                "<ul><li>One</li><li>Two</li></ul>",
                @"
- One
- Two

");
        }

        [Fact]
        public async Task ShouldConvertUnorderedListNestedInUnorderedList()
        {
            await TestConverter(
                "<ul><li>One</li><li>Two</li><ul><li>TwoPointOne</li><li>TwoPointTwo</li></ul><li>Three</li></ul>",
                @"
- One
- Two
    - TwoPointOne
    - TwoPointTwo
- Three

");
        }

        [Fact]
        public async Task ShouldConvertUnorderedListNestedInOrderedList()
        {
            await TestConverter(
                "<ol><li>One</li><li>Two</li><ul><li>TwoPointOne</li><li>TwoPointTwo</li></ul><li>Three</li></ol>",
                @"
1. One
1. Two
    - TwoPointOne
    - TwoPointTwo
1. Three

");
        }

        [Fact]
        public async Task ShouldConvertOrderedListNestedInOrderedList()
        {
            await TestConverter(
                "<ol><li>One</li><li>Two</li><ol><li>TwoPointOne</li><li>TwoPointTwo</li></ol><li>Three</li></ol>",
                @"
1. One
1. Two
    1. TwoPointOne
    1. TwoPointTwo
1. Three

");
        }

        [Fact]
        public async Task ShouldConvertBrTagsInPreTag()
        {
            await TestConverter(
                "<pre>line 1<br />line 2</pre>",
                @"
```
line 1
line 2
```
");
        }

        [Fact]
        public async Task ShouldConvertSimpleOrderedList()
        {
            await TestConverter(
                "<ol><li>One</li><li>Two</li></ol>",
                @"
1. One
1. Two

");
        }

        [Fact]
        public async Task ShouldConvertHeadersAtCorrectLevel()
        {
            for (var i = 1; i <= 6; i++)
            {
                await TestConverter(
                    $"<h{i}>Header</h{i}>",
                    $@"
{new string('#', i)} Header

");
            }
        }

        [Fact]
        public async Task ShouldConvertStylingInHeaders()
        {
            await TestConverter(
                    "<h3>Header <em>Two</em></h3>",
                    @"
### Header *Two*

");
        }

        [Fact]
        public async Task ShouldConvertTableWithHeaderRow()
        {
            await TestConverter(
                    @"<table>
    <thead>
        <tr>
            <th>Col 1</th>
            <th>Col 2</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>1-1</td>
            <td>1-2</td>
        </tr>
        <tr>
            <td>2-1</td>
            <td>2-2</td>
        </tr>
    </tbody>
</table>",
                    $@"
|Col 1|Col 2|
|-|-|
|1\-1|1\-2|
|2\-1|2\-2|

");
        }

        [Fact]
        public async Task ShouldPreventNewLinesAppearingInCells()
        {
            await TestConverter(
                    @"<table>
    <thead>
        <tr>
            <th>Col
1</th>
            <th>Col 2</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>test
test2</td>
            <td><p>test3<p><p>test4" + '\n'+ @"test5</p></td>
        </tr>
    </tbody>
</table>",
                    $@"
|Col 1|Col 2|
|-|-|
|test test2|test3 test4 test5 |

");
        }

        [Fact]
        public async Task ShouldHandleMalformedTableWithTwoBodyTags()
        {
            await TestConverter(
                    @"<table>
    <tbody>
        <tr>
            <td>Col 1</td>
            <td>Col 2</td>
        </tr>
    </tbody>
    <tbody>
        <tr>
            <td>test</td>
            <td>test2</td>
        </tr>
    </tbody>
</table>",
                    $@"
|Col 1|Col 2|
|-|-|
|test|test2|

");
        }

        [Fact]
        public async Task ShouldConvertTableWithNoHeaderRowUsingFirstRowAsHeader()
        {
            await TestConverter(
                    @"<table>
    <tbody>
        <tr>
            <td>1-1</td>
            <td>1-2</td>
        </tr>
        <tr>
            <td>2-1</td>
            <td>2-2</td>
        </tr>
    </tbody>
</table>",
                    $@"
|1\-1|1\-2|
|-|-|
|2\-1|2\-2|

");
        }

        [Fact]
        public async Task ShouldConvertSuccessiveTablesWithoutHeadersWithCorrectColumns()
        {
            await TestConverter(
                    @"
<table>
    <tbody>
        <tr>
            <td>1-1</td>
            <td>1-2</td>
        </tr>
        <tr>
            <td>2-1</td>
            <td>2-2</td>
        </tr>
    </tbody>
</table>

<table>
    <tbody>
        <tr>
            <td>1-1</td>
            <td>1-2</td>
            <td>1-3</td>
        </tr>
        <tr>
            <td>2-1</td>
            <td>2-2</td>
            <td>2-3</td>
        </tr>
    </tbody>
</table>",
                    $@"
|1\-1|1\-2|
|-|-|
|2\-1|2\-2|

|1\-1|1\-2|1\-3|
|-|-|-|
|2\-1|2\-2|2\-3|

");
        }

        [Fact]
        public async Task ShouldConvertPreWithoutClass()
        {
            await TestConverter(
                @"<pre>
line1
line2
</pre>",
                @"
```
line1
line2
```
");
        }

        [Fact]
        public async Task ShouldConvertPreWithDefaultCodeLanguage()
        {
            await TestConverter(
                @"<pre class=""code"">
line1
line2
</pre>",
                @"
``` powershell
line1
line2
```
",
                options: new ConversionOptions { DefaultCodeLanguage = "powershell" });
        }

        [Fact]
        public async Task ShouldStripLeadingAndTrailingReturnsFromPreContent()
        {
            await TestConverter(
                @"<pre class=""code"">

line1
line2
</pre>",
                @"
``` powershell
line1
line2
```
",
                options: new ConversionOptions { DefaultCodeLanguage = "powershell" });
        }

        [Fact]
        public async Task ShouldHtmlDecodePreTagContents()
        {
            await TestConverter(
                @"<pre class=""code"">

&nbsp;&lt;line1&gt;
&nbsp;line2
</pre>",
                @"
``` powershell
 <line1>
 line2
```
",
                options: new ConversionOptions { DefaultCodeLanguage = "powershell" });
        }

        [Fact]
        public async Task ShouldNotEscapeMarkdownCharactersInPreTags()
        {
            await TestConverter(
                @"<pre class=""code"">
-- A comment
</pre>",
                @"
``` powershell
-- A comment
```
",
                options: new ConversionOptions { DefaultCodeLanguage = "powershell" });
        }

        [Fact]
        public async Task ShouldConvertPreWithMappedCodeLanguageAndCodeClass()
        {
            await TestConverter(
                @"<pre class=""code cl-vb"">
line1
line2
</pre>",
                @"
``` vbnet
line1
line2
```
",
                options: new ConversionOptions
                {
                    DefaultCodeLanguage = "powershell",
                    CodeLanguageClassMap =
                    {
                        { "cl-vb", "vbnet" },
                        { "cl-cs", "csharp" },
                    }
                });
        }

        [Fact]
        public async Task ShouldConvertPreWithMappedCodeLanguage()
        {
            await TestConverter(
                @"<pre class=""cl-vb"">
line1
line2
</pre>",
                @"
``` vbnet
line1
line2
```
",
                options: new ConversionOptions
                {
                    DefaultCodeLanguage = "powershell",
                    CodeLanguageClassMap =
                    {
                        { "cl-vb", "vbnet" },
                        { "cl-cs", "csharp" },
                    }
                });
        }

        [Fact]
        public async Task ShouldAddNewLinesBetweenParagraphs()
        {
            await TestConverter(
                @"<p>para one
still in para one</p>
<p>para 2</p>",
                @"para one
still in para one

para 2

");
        }

        [Fact]
        public async Task ShouldConvertSingleLineBlockquote()
        {
            await TestConverter(
                @"<blockquote>text</blockquote>",
                @"
> text

");
        }

        [Fact]
        public async Task ShouldConvertMultiLineBlockquote()
        {
            await TestConverter(
                @"<blockquote>text
text</blockquote>",
                @"
> text
> text

");
        }

        [Fact]
        public async Task ShouldConvertNestedContentInBlockquote()
        {
            await TestConverter(
                @"<blockquote>text
<ul><li>text</li></ul></blockquote>",
                @"
> text
> 
> - text
> 
> 

");
        }

        [Fact]
        public async Task ShouldConvertBlockquoteWithParagraphs()
        {
            await TestConverter(
                @"<blockquote><p>text</p>
<p>text</p></blockquote>",
                @"
> text
> 
> text
> 
> 

");
        }

        [Fact]
        public async Task ShouldFetchImage()
        {
            await TestConverter(
                $@"<img src=""{imageUrl}"" alt=""My image"">",
                @"![My image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(new Uri(pageUrl, imageUrl), imageUrlData)
                });
        }

        [Fact]
        public async Task ShouldFetchRelativeImage()
        {
            await TestConverter(
                $@"<img src=""{relativeImageUrl}"" alt=""My image"">",
                @"![My image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(new Uri(pageUrl, relativeImageUrl), relativeImageUrlData)
                });
        }

        [Fact]
        public async Task ShouldFetchAbsoluteImageFromSameDomain()
        {
            await TestConverter(
                $@"<img src=""{absoluteImageUrlSameDomain}"" alt=""My image"">",
                @"![My image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(absoluteImageUrlSameDomain, absoluteImageUrlSameDomainData)
                });
        }

        [Fact]
        public async Task ShouldHandleMissingImagesByNotCollectingThem()
        {
            await TestConverter(
                $@"<img src=""{missingImageUrl}"" alt=""My image"">",
                @"![My image](missing.png)");
        }

        [Fact]
        public async Task ShouldNotFetchAbsoluteImageFromDifferentDomain()
        {
            await TestConverter(
                $@"<img src=""{absoluteImageUrlDifferentDomain}"" alt=""My image"">",
                $@"![My image]({absoluteImageUrlDifferentDomain})");
        }

        [Fact]
        public async Task ShouldRenderBoldLinkToExternalSite()
        {
            await TestConverter(
                $@"<strong>Open <a href=""https://github.com/"">Github</a></strong>",
                @"**Open [Github](https://github.com/)**");
        }

        [Fact]
        public async Task ShouldFetchLinkedImage()
        {
            await TestConverter(
                $@"<a href=""{imageUrl}"">Linked image</a>",
                @"[Linked image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(new Uri(pageUrl, imageUrl), imageUrlData)
                });
        }

        [Fact]
        public async Task ShouldFetchLinkedRelativeImage()
        {
            await TestConverter(
                $@"<a href=""{relativeImageUrl}"">Linked image</a>",
                @"[Linked image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(new Uri(pageUrl, relativeImageUrl), relativeImageUrlData)
                });
        }

        [Fact]
        public async Task ShouldFetchLinkedAbsoluteImageFromSameDomain()
        {
            await TestConverter(
                $@"<a href=""{absoluteImageUrlSameDomain}"">Linked image</a>",
                @"[Linked image](img.png)",
                expectedImages: new[] {
                    new ReferencedImage(absoluteImageUrlSameDomain, absoluteImageUrlSameDomainData)
                });
        }

        [Fact]
        public async Task ShouldNotFetchLinkedAbsoluteImageFromDifferentDomain()
        {
            await TestConverter(
                $@"<a href=""{absoluteImageUrlDifferentDomain}"">Linked image</a>",
                $@"[Linked image]({absoluteImageUrlDifferentDomain})");
        }

        private static async Task TestConverter(
            IReadOnlyList<string> pageContent,
            IReadOnlyList<string> expectedMarkdown,
            ConversionOptions options = null,
            IReadOnlyList<ReferencedImage> expectedImages = null)
        {
            var results = await ExecuteConverter(pageContent, options);

            for (var i = 0; i < pageContent.Count; i++)
            {
                results.Documents[i].Markdown.Should().Be(expectedMarkdown[i]);
            }

            results.Images.Should().BeEquivalentTo(expectedImages ?? Array.Empty<ReferencedImage>());
        }

        private static async Task<ConvertionResult> ExecuteConverter(IReadOnlyList<string> pageContent, ConversionOptions options = null)
        {
            var mockHandler = CreateMockHttpMessageHandler(pageContent.ToArray());

            options ??= new ConversionOptions();

            var converter = new MarkdownConverter(options, httpClient: new HttpClient(mockHandler));

            return await converter.ConvertAsync(Enumerable.Range(0, pageContent.Count).Select(ConstructPageUriForIndex));
        }

        private static async Task TestConverter(
            string pageContent,
            string expectedMarkdown,
            ConversionOptions options = null,
            IReadOnlyList<ReferencedImage> expectedImages = null)
        {
            var results = await ExecuteConverter(pageContent, options);

            results.Documents.Single().Markdown.Should().Be(expectedMarkdown);
            results.Images.Should().BeEquivalentTo(expectedImages ?? Array.Empty<ReferencedImage>());
        }

        private static async Task<ConvertionResult> ExecuteConverter(string pageContent, ConversionOptions options = null)
        {
            var mockHandler = CreateMockHttpMessageHandler(pageContent);

            options ??= new ConversionOptions();

            var converter = new MarkdownConverter(options, httpClient: new HttpClient(mockHandler));

            return await converter.ConvertAsync(pageUrl);
        }

        private static MockHttpMessageHandler CreateMockHttpMessageHandler(params string[] pageContent)
        {
            var mockHttp = new MockHttpMessageHandler();

            for (var i = 0; i < pageContent.Length; i++)
            {
                mockHttp.When(ConstructPageUriForIndex(i).AbsoluteUri).Respond("text/html", pageContent[i]);
            }

            mockHttp.When(absoluteImageUrlDifferentDomain.AbsoluteUri).Respond(
                r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(absoluteImageUrlDifferentDomainData) });
            mockHttp.When(absoluteImageUrlSameDomain.AbsoluteUri).Respond(
                r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(absoluteImageUrlSameDomainData) });
            mockHttp.When(new Uri(pageUrl, relativeImageUrl).AbsoluteUri).Respond(
                r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(relativeImageUrlData) });
            mockHttp.When(new Uri(pageUrl, imageUrl).AbsoluteUri).Respond(
                r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(imageUrlData) });
            mockHttp.When(new Uri(pageUrl, missingImageUrl).AbsoluteUri).Respond(HttpStatusCode.NotFound);
            return mockHttp;
        }

        private static Uri ConstructPageUriForIndex(int index)
        {
            return new Uri(pageUrl.AbsoluteUri + (index == 0 ? "" : (index + 1).ToString()));
        }
    }
}
