﻿using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Html2md.Tests.Unit
{
    public class MarkdownConverterTests
    {
        private static readonly Uri pageUrl = new Uri("https://converttest.goatly.net/page/name");

        [Fact]
        public async Task ShouldConvertEmptyPageToEmptyMarkdown()
        {
            await TestConverter(
                "", 
                "");
        }

        [Fact]
        public async Task ShouldConvertEm()
        {
            await TestConverter(
                "<em>test</em>",
                "*test*");
        }

        [Fact]
        public async Task ShouldConvertStrong()
        {
            await TestConverter(
                "<strong>test</strong>",
                "**test**");
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

        private static async Task TestConverter(
            string pageContent, 
            string expectedMarkdown, 
            ConversionOptions options = null,
            IReadOnlyList<ReferencedImage> expectedImages = null)
        {
            var results = await ExecuteConverter(pageContent, options);

            results.Markdown.Should().Be(expectedMarkdown);
            results.Images.Should().BeEquivalentTo(expectedImages ?? Array.Empty<ReferencedImage>());
        }

        private static async Task<ConvertedDocument> ExecuteConverter(string pageContent, ConversionOptions options = null)
        {
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(pageUrl.AbsoluteUri)
                .Respond("text/html", pageContent);

            options ??= new ConversionOptions();

            var converter = new MarkdownConverter(options, httpClient: new HttpClient(mockHttp));

            return await converter.ConvertAsync(pageUrl);
        }
    }
}
