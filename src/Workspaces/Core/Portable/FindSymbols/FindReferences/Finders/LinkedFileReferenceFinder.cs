﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols.Finders
{
    internal class LinkedFileReferenceFinder : IReferenceFinder
    {
        public async Task<IEnumerable<SymbolAndProjectId>> DetermineCascadedSymbolsAsync(
            SymbolAndProjectId symbolAndProjectId, Solution solution, IImmutableSet<Project> projects = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var linkedSymbols = new HashSet<SymbolAndProjectId>();

            var symbol = symbolAndProjectId.Symbol;
            foreach (var location in symbol.DeclaringSyntaxReferences)
            {
                var originalDocument = solution.GetDocument(location.SyntaxTree);

                // GetDocument will return null for locations in #load'ed trees.
                // TODO:  Remove this check and add logic to fetch the #load'ed tree's
                // Document once https://github.com/dotnet/roslyn/issues/5260 is fixed.
                if (originalDocument == null)
                {
                    Debug.Assert(solution.Workspace.Kind == "Interactive");
                    continue;
                }

                foreach (var linkedDocumentId in originalDocument.GetLinkedDocumentIds())
                {
                    var linkedDocument = solution.GetDocument(linkedDocumentId);
                    var linkedSyntaxRoot = await linkedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                    // Defend against constructed solutions with inconsistent linked documents
                    if (!linkedSyntaxRoot.FullSpan.Contains(location.Span))
                    {
                        continue;
                    }

                    var linkedNode = linkedSyntaxRoot.FindNode(location.Span, getInnermostNodeForTie: true);

                    var semanticModel = await linkedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                    var linkedSymbol = semanticModel.GetDeclaredSymbol(linkedNode, cancellationToken);

                    if (linkedSymbol != null &&
                        linkedSymbol.Kind == symbol.Kind &&
                        linkedSymbol.Name == symbol.Name)
                    {
                        var linkedSymbolAndProjectId = SymbolAndProjectId.Create(linkedSymbol, linkedDocument.Project.Id);
                        if (!linkedSymbols.Contains(linkedSymbolAndProjectId))
                        {
                            linkedSymbols.Add(linkedSymbolAndProjectId);
                        }
                    }
                }
            }

            return linkedSymbols;
        }

        public Task<IEnumerable<Document>> DetermineDocumentsToSearchAsync(ISymbol symbol, Project project, IImmutableSet<Document> documents, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SpecializedTasks.EmptyEnumerable<Document>();
        }

        public Task<IEnumerable<Project>> DetermineProjectsToSearchAsync(ISymbol symbol, Solution solution, IImmutableSet<Project> projects = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SpecializedTasks.EmptyEnumerable<Project>();
        }

        public Task<IEnumerable<ReferenceLocation>> FindReferencesInDocumentAsync(
            SymbolAndProjectId symbolAndProjectId, Document document, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SpecializedTasks.EmptyEnumerable<ReferenceLocation>();
        }
    }
}
