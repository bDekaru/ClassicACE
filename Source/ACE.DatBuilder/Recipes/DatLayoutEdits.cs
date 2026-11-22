using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.Entity.Enum;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// One changed media item of a UI layout: the element as the hex ids from the layout's top element down to it, joined by '/', the id of the state whose media it is (empty for the element's own media),
    /// the index in the media list and the new file id as hex text.
    /// </summary>
    public class DatLayoutMediaEdit
    {
        public string Element { get; set; }
        public string State { get; set; }
        public int Media { get; set; }
        public string File { get; set; }
    }

    /// <summary>A new position of an element of a UI layout, in the layout's pixels; the element is named as in <see cref="DatLayoutMediaEdit"/>.</summary>
    public class DatLayoutMoveEdit
    {
        public string Element { get; set; }
        public uint X { get; set; }
        public uint Y { get; set; }
    }

    /// <summary>Edits of a UI layout record (language dat, 0x21......), read from a recipe data file.</summary>
    public static class DatLayoutEdits
    {
        /// <summary>The MasterProperty record of the portal dat, which gives the type of every property a layout holds.</summary>
        public const uint MasterPropertyId = 0x39000001;

        public static LayoutDesc Unpack(byte[] record, MasterProperty master)
        {
            var layout = new LayoutDesc();

            using (var reader = new BinaryReader(new MemoryStream(record)))
                layout.Unpack(reader, id => master.m_properties[id].m_propertyType);

            return layout;
        }

        /// <exception cref="InvalidDataException">An edit names an element, state or media item the layout does not have.</exception>
        public static void Apply(LayoutDesc layout, List<DatLayoutMediaEdit> edits)
        {
            foreach (var edit in edits)
            {
                var element = Find(layout, edit.Element);
                StateDesc state = element;

                if (!string.IsNullOrEmpty(edit.State) && !element.States.TryGetValue(Parse(edit.State), out state))
                    throw new InvalidDataException($"Layout edit: element {edit.Element} has no state {edit.State}.");

                if (edit.Media < 0 || edit.Media >= state.Media.Count)
                    throw new InvalidDataException($"Layout edit: element {edit.Element} state {edit.State} has no media item {edit.Media}.");

                state.Media[edit.Media].File = Parse(edit.File);
            }
        }

        /// <summary>Moves elements to a new position (the client needs every element of a page to exist, so an unused one is moved out of sight instead of removed).</summary>
        /// <exception cref="InvalidDataException">An edit names an element the layout does not have.</exception>
        public static void Move(LayoutDesc layout, List<DatLayoutMoveEdit> edits)
        {
            foreach (var edit in edits)
            {
                var element = Find(layout, edit.Element);

                element.UiIncorporationFlags |= IncorporationFlags.X | IncorporationFlags.Y;
                element.X = edit.X;
                element.Y = edit.Y;
            }
        }

        private static ElementDesc Find(LayoutDesc layout, string path)
        {
            var ids = path.Split('/').Select(Parse).ToList();

            if (!layout.Elements.TryGetValue(ids[0], out var element))
                throw new InvalidDataException($"Layout edit: the layout has no element {path}.");

            foreach (var id in ids.Skip(1))
            {
                if (!element.Children.TryGetValue(id, out element))
                    throw new InvalidDataException($"Layout edit: the layout has no element {path}.");
            }

            return element;
        }

        /// <summary>The edits that turn <paramref name="source"/> into <paramref name="target"/>, in the order of the layout (the tool that writes a data file from an existing record). Only changes of the file id of a media item are expressed.</summary>
        /// <exception cref="InvalidDataException">The layouts differ in structure.</exception>
        public static List<DatLayoutMediaEdit> Diff(LayoutDesc source, LayoutDesc target)
        {
            var edits = new List<DatLayoutMediaEdit>();

            if (source.Elements.Count != target.Elements.Count)
                throw new InvalidDataException("Layout edits can only be made between layouts with the same elements.");

            foreach (var element in source.Elements)
                DiffElement(element.Value, target.Elements[element.Key], Hex(element.Key), edits);

            return edits;
        }

        private static void DiffElement(ElementDesc source, ElementDesc target, string path, List<DatLayoutMediaEdit> edits)
        {
            DiffState(source, target, path, "", edits);

            if (source.States.Count != target.States.Count || source.Children.Count != target.Children.Count)
                throw new InvalidDataException($"Layout edits: element {path} has a different number of states or children.");

            foreach (var state in source.States)
                DiffState(state.Value, target.States[state.Key], path, Hex(state.Key), edits);

            foreach (var child in source.Children)
                DiffElement(child.Value, target.Children[child.Key], path + "/" + Hex(child.Key), edits);
        }

        private static void DiffState(StateDesc source, StateDesc target, string path, string state, List<DatLayoutMediaEdit> edits)
        {
            if (source.Media.Count != target.Media.Count)
                throw new InvalidDataException($"Layout edits: element {path} state {state} has a different number of media items.");

            for (var i = 0; i < source.Media.Count; i++)
            {
                if (source.Media[i].File != target.Media[i].File)
                    edits.Add(new DatLayoutMediaEdit { Element = path, State = state, Media = i, File = Hex(target.Media[i].File) });
            }
        }

        private static string Hex(uint value)
        {
            return value.ToString("X8");
        }

        private static uint Parse(string value)
        {
            return uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}
