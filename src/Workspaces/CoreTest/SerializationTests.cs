﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;
using CS = Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.UnitTests
{
    [UseExportProvider]
    public partial class SerializationTests : TestBase
    {
        private Document CreateSolutionDocument(string sourceText)
        {
            var pid = ProjectId.CreateNewId();
            var did = DocumentId.CreateNewId(pid);

            var solution = new AdhocWorkspace().CurrentSolution
                    .AddProject(pid, "test", "test", LanguageNames.CSharp)
                    .AddMetadataReference(pid, TestReferences.NetFx.v4_0_30319.mscorlib)
                    .AddDocument(did, "goo.cs", SourceText.From(sourceText));

            return solution.GetDocument(did);
        }

        [Fact]
        public void TestNameSimplificationAnnotationSerialization()
        {
            var text = @"public class C {}";
            var doc = CreateSolutionDocument(text);
            TestSymbolSerialization(doc, "C");
        }

        [Fact]
        public void VersionStamp_RoundTripText()
        {
            var versionStamp = VersionStamp.Create();

            using var writerStream = new MemoryStream();

            using (var writer = new ObjectWriter(writerStream, leaveOpen: true))
            {
                versionStamp.WriteTo(writer);
            }

            using var readerStream = new MemoryStream(writerStream.ToArray());
            using var reader = ObjectReader.TryGetReader(readerStream);
            var deserializedVersionStamp = VersionStamp.ReadFrom(reader);

            Assert.Equal(versionStamp, deserializedVersionStamp);
        }

        private void TestSymbolSerialization(Document document, string symbolName)
        {
            var model = document.GetSemanticModelAsync().Result;
            var name = CS.SyntaxFactory.ParseName(symbolName);
            var symbol = model.GetSpeculativeSymbolInfo(0, name, SpeculativeBindingOption.BindAsExpression).Symbol;

            var root = (CS.Syntax.CompilationUnitSyntax)model.SyntaxTree.GetRoot();
            var annotation = SymbolAnnotation.Create(symbol);
            var rootWithAnnotation = root.WithAdditionalAnnotations(annotation);
            Assert.True(rootWithAnnotation.ContainsAnnotations);
            Assert.True(rootWithAnnotation.HasAnnotation(annotation));

            var stream = new MemoryStream();
            rootWithAnnotation.SerializeTo(stream);

            stream.Position = 0;
            var droot = CS.CSharpSyntaxNode.DeserializeFrom(stream);
            Assert.True(droot.ContainsAnnotations);
            Assert.True(droot.HasAnnotation(annotation));

            var dannotation = droot.GetAnnotations(SymbolAnnotation.Kind).SingleOrDefault();
            Assert.NotNull(dannotation);
            Assert.NotSame(annotation, dannotation);
            Assert.Equal(annotation, dannotation);
            var id = SymbolAnnotation.GetSymbol(annotation, model.Compilation);
            var did = SymbolAnnotation.GetSymbol(dannotation, model.Compilation);

            Assert.True(id.Equals(did));
        }
    }
}
