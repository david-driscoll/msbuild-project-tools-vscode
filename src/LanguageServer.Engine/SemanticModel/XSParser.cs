using Microsoft.Language.Xml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    using Utilities;

    /// <summary>
    ///     Parses the syntax model to derive a semantic model.
    /// </summary>
    public static class XSParser
    {
        /// <summary>
        ///     Parse the syntax model to derive a semantic model.
        /// </summary>
        /// <param name="document">
        ///     The <see cref="XmlDocumentSyntax"/> to parse.
        /// </param>
        /// <param name="xmlPositions">
        ///     The lookup for document positions.
        /// </param>
        /// <returns>
        ///     A list of <see cref="XSNode"/>s, sorted by <see cref="XSNode.Range"/>.
        /// </returns>
        public static List<XSNode> GetSemanticModel(this XmlDocumentSyntax document, TextPositions xmlPositions)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (xmlPositions == null)
                throw new ArgumentNullException(nameof(xmlPositions));

            XSParserVisitor parserVisitor = new XSParserVisitor(xmlPositions);
            parserVisitor.Visit(document);
            parserVisitor.CalculateWhitespace();

            return new List<XSNode>(
                parserVisitor.DiscoveredNodes
                    .OrderBy(discoveredNode => discoveredNode.Range.Start)
                    .ThenBy(discoveredNode => discoveredNode.Range.End)
            );
        }

        /// <summary>
        ///     Parse the syntax model to derive a semantic model.
        /// </summary>
        /// <param name="node">
        ///     The <see cref="SyntaxNode"/> to parse.
        /// </param>
        /// <param name="xmlPositions">
        ///     The lookup for document positions.
        /// </param>
        /// <returns>
        ///     A list of <see cref="XSNode"/>s, sorted by <see cref="XSNode.Range"/>.
        /// </returns>
        public static List<XSNode> GetSemanticModel(this XmlNodeSyntax node, TextPositions xmlPositions)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (xmlPositions == null)
                throw new ArgumentNullException(nameof(xmlPositions));

            XSParserVisitor parserVisitor = new XSParserVisitor(xmlPositions);
            parserVisitor.Visit(node);
            parserVisitor.CalculateWhitespace();

            return new List<XSNode>(
                parserVisitor.DiscoveredNodes
                    .OrderBy(discoveredNode => discoveredNode.Range.Start)
                    .ThenBy(discoveredNode => discoveredNode.Range.End)
            );
        }

        /// <summary>
        ///     A syntax visitor that extracts a semantic model (in the form of <see cref="XSNode"/>s) from syntax nodes.
        /// </summary>
        class XSParserVisitor
            : SyntaxVisitor
        {
            /// <summary>
            ///     Spans for whitespace that has already been processed.
            /// </summary>
            readonly SortedSet<TextSpan> _whitespaceSpans = new SortedSet<TextSpan>();

            /// <summary>
            ///     The stack of <see cref="XSElement"/>s being processed.
            /// </summary>
            readonly Stack<XSElement> _elementStack = new Stack<XSElement>();

            /// <summary>
            ///     Lookup for document text positions.
            /// </summary>
            readonly TextPositions _textPositions;

            /// <summary>
            ///     The index of the current element (if any) in the list of discovered nodes.
            /// </summary>
            int _currentElementIndex = -1;

            /// <summary>
            ///     Create a new <see cref="XSParserVisitor"/>.
            /// </summary>
            /// <param name="textPositions">
            ///     Lookup for document text positions.
            /// </param>
            public XSParserVisitor(TextPositions textPositions)
            {
                if (textPositions == null)
                    throw new ArgumentNullException(nameof(textPositions));

                _textPositions = textPositions;
            }

            /// <summary>
            ///     <see cref="XSNode"/>s discovered in the XML.
            /// </summary>
            public List<XSNode> DiscoveredNodes { get; } = new List<XSNode>();

            /// <summary>
            ///     Is there an element currently being processed?
            /// </summary>
            bool HaveCurrentElement => _elementStack.Count > 0;

            /// <summary>
            ///     The element (if any) being processed.
            /// </summary>
            XSElement CurrentElement => HaveCurrentElement ? _elementStack.Peek() : null;

            /// <summary>
            ///     Find the spaces between elements and use that to infer whitespace.
            /// </summary>
            public void CalculateWhitespace()
            {
                Queue<XSElement> discoveredElements = new Queue<XSElement>(
                    DiscoveredNodes.OfType<XSElement>()
                        .OrderBy(discoveredNode => discoveredNode.Range.Start)
                        .ThenBy(discoveredNode => discoveredNode.Range.End)
                );

                while (discoveredElements.Count > 1) // AF: What about the last one?
                {
                    XSElement element1 = discoveredElements.Dequeue();
                    XSElement element2 = discoveredElements.Peek();

                    int charsBetweenElements;
                    if (element2.ParentElement == element1)
                    {
                        XSElementWithContent element1WithContent = (XSElementWithContent)element1;

                        int element1OpeningTagEnd = element1WithContent.ElementNode.StartTag.End;
                        charsBetweenElements = element2.ElementNode.Start - element1OpeningTagEnd; 

                        continue;
                    }

                    discoveredElements.Dequeue();

                    charsBetweenElements = element2.ElementNode.Start - element1.ElementNode.End;
                    if (charsBetweenElements != 0)
                        System.Diagnostics.Debugger.Break();
                    
                }
            }

            /// <summary>
            ///     Visit an <see cref="XmlDocumentSyntax"/>.
            /// </summary>
            /// <param name="document">
            ///     The <see cref="XmlDocumentSyntax"/>.
            /// </param>
            /// <returns>
            ///     The <see cref="XmlDocumentSyntax"/> (unchanged).
            /// </returns>
            public override SyntaxNode VisitXmlDocument(XmlDocumentSyntax document)
            {
                foreach (XmlElementSyntaxBase element in document.Elements.OfType<XmlElementSyntaxBase>())
                    Visit(element);

                return document;
            }

            /// <summary>
            ///     Visit an <see cref="XmlElementSyntax"/>.
            /// </summary>
            /// <param name="element">
            ///     The <see cref="XmlElementSyntax"/>.
            /// </param>
            /// <returns>
            ///     The <see cref="XmlElementSyntax"/> (unchanged).
            /// </returns>
            public override SyntaxNode VisitXmlElement(XmlElementSyntax element)
            {
                Range elementRange = element.Span.ToNative(_textPositions);
                Range openingTagRange = element.StartTag?.Span.ToNative(_textPositions) ?? elementRange;
                Range closingTagRange = element.EndTag?.Span.ToNative(_textPositions) ?? elementRange;
                Range contentRange;
                if (openingTagRange.End <= closingTagRange.Start)
                {
                    contentRange = new Range(
                        start: openingTagRange.End,
                        end: closingTagRange.Start
                    );
                }
                else
                    contentRange = elementRange;

                XSElement xsElement;
                if (String.IsNullOrWhiteSpace(element.Name) || openingTagRange == elementRange || contentRange == elementRange || closingTagRange == elementRange)
                    xsElement = new XSInvalidElement(element, elementRange, parent: CurrentElement, hasContent: true);
                else
                    xsElement = new XSElementWithContent(element, elementRange, openingTagRange, contentRange, closingTagRange, parent: CurrentElement);

                PushElement(xsElement);

                foreach (XmlAttributeSyntax attribute in element.AsSyntaxElement.Attributes)
                    Visit(attribute);

                Position previousTagEnd = _textPositions.GetPosition(element.StartTag.End);
                foreach (XmlElementSyntaxBase childElement in element.Elements.OfType<XmlElementSyntaxBase>())
                {
                    Range leadingWhitespaceRange = new Range(
                        start: previousTagEnd,
                        end: _textPositions.GetPosition(childElement.Start)
                    );
                    if (!leadingWhitespaceRange.IsEmpty)
                    {
                        DiscoveredNodes.Add(
                            new XSWhitespace(leadingWhitespaceRange, xsElement)
                        );
                    }

                    Visit(childElement);
                }

                PopElement();

                return element;
            }

            /// <summary>
            ///     Visit an <see cref="XmlEmptyElementSyntax"/>.
            /// </summary>
            /// <param name="emptyElement">
            ///     The <see cref="XmlEmptyElementSyntax"/>.
            /// </param>
            /// <returns>
            ///     The <see cref="XmlEmptyElementSyntax"/> (unchanged).
            /// </returns>
            public override SyntaxNode VisitXmlEmptyElement(XmlEmptyElementSyntax emptyElement)
            {
                Range elementRange = emptyElement.Span.ToNative(_textPositions);

                XSElement xsElement;
                if (String.IsNullOrWhiteSpace(emptyElement.Name))
                    xsElement = new XSInvalidElement(emptyElement, elementRange, parent: CurrentElement, hasContent: false);
                else
                    xsElement = new XSEmptyElement(emptyElement, elementRange, parent: CurrentElement);

                PushElement(xsElement);

                foreach (XmlAttributeSyntax attribute in emptyElement.AsSyntaxElement.Attributes)
                    Visit(attribute);

                PopElement();

                return emptyElement;
            }

            /// <summary>
            ///     Visit an <see cref="XmlAttributeSyntax"/>.
            /// </summary>
            /// <param name="attribute">
            ///     The <see cref="XmlAttributeSyntax"/>.
            /// </param>
            /// <returns>
            ///     The <see cref="XmlAttributeSyntax"/> (unchanged).
            /// </returns>
            public override SyntaxNode VisitXmlAttribute(XmlAttributeSyntax attribute)
            {
                if (!HaveCurrentElement)
                    return base.VisitXmlAttribute(attribute);

                Range attributeRange = attribute.Span.ToNative(_textPositions);
                Range nameRange = attribute.NameNode?.Span.ToNative(_textPositions) ?? attributeRange;
                Range valueRange = attribute.ValueNode?.Span.ToNative(_textPositions) ?? nameRange;

                XSAttribute xsAttribute;
                if (String.IsNullOrWhiteSpace(attribute.Name) || nameRange == attributeRange || valueRange == attributeRange)
                    xsAttribute = new XSInvalidAttribute(attribute, attributeRange, nameRange, valueRange, CurrentElement);
                else
                    xsAttribute = new XSAttribute(attribute, attributeRange, nameRange, valueRange, CurrentElement);

                ModifyCurrentElement(
                    currentElement => currentElement.WithAttribute(xsAttribute)
                );

                // Add the re-parented attribute, not the original.
                xsAttribute = CurrentElement.Attributes[CurrentElement.Attributes.Count - 1];
                DiscoveredNodes.Add(xsAttribute);

                return base.VisitXmlAttribute(attribute);
            }

            /// <summary>
            ///     Visit an <see cref="XmlTextSyntax"/>.
            /// </summary>
            /// <param name="text">
            ///     The <see cref="XmlTextSyntax"/>.
            /// </param>
            /// <returns>
            ///     The <see cref="XmlTextSyntax"/> (unchanged).
            /// </returns>
            public override SyntaxNode VisitXmlText(XmlTextSyntax text)
            {
                Range textRange = text.Span.ToNative(_textPositions);
                if (CurrentElement == null || !CurrentElement.Range.Contains(textRange))
                    return text;

                DiscoveredNodes.Add(
                    new XSElementText(text, textRange, CurrentElement)
                );

                return text;
            }

            /// <summary>
            ///     Modify the current element.
            /// </summary>
            /// <param name="modification">
            ///     A delegate that takes the current element, and returns a modified element.
            /// </param>
            void ModifyCurrentElement(Func<XSElement, XSElement> modification)
            {
                if (modification == null)
                    throw new ArgumentNullException(nameof(modification));

                if (!HaveCurrentElement)
                    throw new InvalidOperationException("No current element.");
                
                XSElement modified = modification(
                    _elementStack.Pop()
                );
                _elementStack.Push(modified);

                DiscoveredNodes[_currentElementIndex] = modified;
            }

            /// <summary>
            ///     Push an <see cref="XSElement"/> onto the stack.
            /// </summary>
            /// <param name="element">
            ///     The <see cref="XSElement"/> being processed.
            /// </param>
            /// <returns>
            ///     The <see cref="XSElement"/>.
            /// </returns>
            XSElement PushElement(XSElement element)
            {
                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                _elementStack.Push(element);

                DiscoveredNodes.Add(element);
                _currentElementIndex = DiscoveredNodes.Count - 1;                

                return element;
            }

            /// <summary>
            ///     Pop an element from the stack.
            /// </summary>
            void PopElement()
            {
                _elementStack.Pop();

                _currentElementIndex = DiscoveredNodes.Count - 1;
            }
        }
    }
}
