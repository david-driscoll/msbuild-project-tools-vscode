﻿using Microsoft.Language.Xml;

namespace MSBuildProjectTools.LanguageServer.SemanticModel
{
    /// <summary>
    ///     Represents a invalid XML element (with or without content).
    /// </summary>
    public class XSInvalidElement
        : XSElement
    {
        /// <summary>
        ///     Create a new <see cref="XSInvalidElement"/>.
        /// </summary>
        /// <param name="element">
        ///     The <see cref="XmlElementSyntaxBase"/> represented by the <see cref="XSInvalidElement"/>.
        /// </param>
        /// <param name="range">
        ///     The <see cref="Range"/>, within the source text, spanned by the element.
        /// </param>
        /// <param name="parent">
        ///     The <see cref="XSInvalidElement"/>'s parent element (if any).
        /// </param>
        /// <param name="hasContent">
        ///     Does the <see cref="XSInvalidElement"/> have any content (besides attributes)?
        /// </param>
        public XSInvalidElement(XmlElementSyntaxBase element, Range range, XSElement parent, bool hasContent)
            : base(element, range, parent)
        {
            HasContent = hasContent;
        }

        /// <summary>
        ///     Does the <see cref="XSNode"/> represent valid XML?
        /// </summary>
        public override bool IsValid => false;

        /// <summary>
        ///     Does the <see cref="XSElement"/> have any content (besides attributes)?
        /// </summary>
        public override bool HasContent { get; }

        /// <summary>
        ///     Clone the <see cref="XSInvalidElement"/>.
        /// </summary>
        /// <returns>
        ///     The clone.
        /// </returns>
        protected override XSNode Clone() => new XSInvalidElement(ElementNode, Range, ParentElement, HasContent);
    }
}
