﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using OpenVIII.AV;
using OpenVIII.Encoding.Tags;
using OpenVIII.IGMData.Dialog;
using OpenVIII.IGMDataItem;

namespace OpenVIII.IGMData
{
    public class PartyItems : Base
    {
        #region Fields

        private readonly FF8String DialogSelectedItem;
        private readonly FF8String str_NotFound;
        private readonly FF8String str_Over100;
        private readonly FF8String str_Recieved;
        private ConcurrentQueue<KeyValuePair<Cards.ID, byte>> _cards;
        private Saves.Item _item;
        private KeyValuePair<Cards.ID, byte> card;

        #endregion Fields

        #region Constructors

        public PartyItems()
        {
            str_NotFound = Strings.Name.Items_NotFound;
            str_Over100 = Strings.Name.Items_Over100;
            str_Recieved = Strings.Name.Items_Recieved;
            DialogSelectedItem = new[] { (byte)FF8TextTagCode.Dialog, (byte)FF8TextTagDialog.SelectedItem };
        }

        #endregion Constructors

        #region Properties

        public Saves.Item Item { get => _item; private set => _item = value; }

        public ConcurrentQueue<Saves.Item> Items { get; private set; }

        #endregion Properties

        #region Methods

        public static PartyItems Create(Rectangle pos) => Create<PartyItems>(1, 7, new Empty { Pos = pos }, 1, 1);

        public void Earn()
        {
            skipsnd = true;
            Sound.Play(17);
        }

        public override bool Inputs_CANCEL() => false;

        public override bool Inputs_OKAY()
        {
            if (ITEM[0, 5].Enabled || ITEM[0, 6].Enabled)
            {
                if (Items != null && Items.Count > 0 || _cards != null && _cards.Count > 0)
                {
                    Refresh();
                    base.Inputs_OKAY();
                    return true;
                }
            }
            else if (Items != null && Items.Count > 0)
            {
                if (Items.TryDequeue(out var item) && Memory.State.Items.FirstOrDefault(x=>x.ID == item.ID).QTY < Memory.State.EarnItem(item).QTY)
                {
                    ITEM?[0, 6]?.Show();
                    Earn();
                }
                else
                {
                    ITEM?[0, 5]?.Show();
                    Earn();
                }

                base.Inputs_OKAY();
                return true;
            }
            else if (_cards != null && _cards.Count > 0)
            {
                if (_cards.TryDequeue(out var card) && Memory.State.EarnItem(card))
                {
                    ITEM?[0, 6]?.Show();
                    Earn();
                }
                else
                {
                    ITEM?[0, 5]?.Show();
                    Earn();
                }

                base.Inputs_OKAY();
                return true;
            }
            return false;
        }

        public override void Refresh()
        {
            base.Refresh();
            if (Items != null && Items.TryPeek(out _item))
            {
                ((Box)ITEM[0, 1]).Data = Item.Data?.Name;
                ((Box)ITEM[0, 2]).Data = $"{Item.QTY}";
                ((Box)ITEM[0, 3]).Data = Item.Data?.Description;
                ((Small)ITEM[0, 5]).Data = str_Over100.Clone().Replace(DialogSelectedItem, Item.Data?.Name ?? "");
                ((Small)ITEM[0, 5]).Data = str_Over100.Clone().Replace(DialogSelectedItem, Item.Data?.Name ?? "");
                ITEM[0, 1].Show();
                ITEM[0, 2].Show();
                ITEM[0, 3].Show();
                ITEM[0, 4].Hide();
                ITEM[0, 5].Hide();
                ITEM[0, 6].Hide();
            }
            else
            if (_cards != null && _cards.TryPeek(out card))
            {
                var name = Memory.Strings.Read(Strings.FileID.MenuGroup, 110, (int)card.Key);
                var pos = 0;
                for (; pos < name.Length; pos++)
                    if (name.Value[pos] == 2) break;
                var trimname = new FF8String(name.Value.Take(pos - 1).ToArray());
                ((Box)ITEM[0, 1]).Data = trimname;
                //TODO grab card name from start of string
                ((Box)ITEM[0, 2]).Data = $"{card.Value}";
                ((Box)ITEM[0, 3]).Data = "";
                ((Small)ITEM[0, 5]).Data = str_Over100.Clone().Replace(DialogSelectedItem, trimname); 
                ((Small)ITEM[0, 5]).Data = str_Over100.Clone().Replace(DialogSelectedItem, trimname);
                ITEM[0, 1].Show();
                ITEM[0, 2].Show();
                ITEM[0, 3].Hide();
                ITEM[0, 4].Hide();
                ITEM[0, 5].Hide();
                ITEM[0, 6].Hide();
            }
            else
            {
                ITEM?[0, 1]?.Hide();
                ITEM?[0, 2]?.Hide();
                ITEM?[0, 3]?.Hide();
                ITEM?[0, 4]?.Show();
                ITEM?[0, 5]?.Hide();
                ITEM?[0, 6]?.Hide();
            }
        }

        public void SetItems(ConcurrentDictionary<Cards.ID, byte> cards)
        {
            if (cards.Count > 0)
            {
                _cards = new ConcurrentQueue<KeyValuePair<Cards.ID, byte>>();
                foreach (var e in cards)
                    _cards.Enqueue(e);
            }
            else _cards = null;
        }

        public void SetItems(ConcurrentDictionary<byte, byte> items)
        {
            if (items.Count > 0)
            {
                Items = new ConcurrentQueue<Saves.Item>();
                foreach (var e in items)
                    Items.Enqueue(new Saves.Item(e));
            }
            else Items = null;
        }

        protected override void Init()
        {
            base.Init();
            Hide();
            ITEM[0, 0] = new Box { Data = Strings.Name.Items_Recieved, Pos = new Rectangle(SIZE[0].X, SIZE[0].Y, SIZE[0].Width, 78), Title = Icons.ID.INFO, Options = Box_Options.Middle };
            ITEM[0, 1] = new Box { Pos = new Rectangle(SIZE[0].X + 140, SIZE[0].Y + 189, 475, 78), Title = Icons.ID.ITEM, Options = Box_Options.Middle }; // item name
            ITEM[0, 2] = new Box { Pos = new Rectangle(SIZE[0].X + 615, SIZE[0].Y + 189, 125, 78), Title = Icons.ID.NUM_, Options = Box_Options.Middle | Box_Options.Center }; // item Count
            ITEM[0, 3] = new Box { Pos = new Rectangle(SIZE[0].X, SIZE[0].Y + 444, SIZE[0].Width, 78), Title = Icons.ID.HELP, Options = Box_Options.Middle }; // item description
            ITEM[0, 4] = Small.Create(null, SIZE[0].X + 232, SIZE[0].Y + 315, Icons.ID.NOTICE, Box_Options.Center | Box_Options.Middle, SIZE[0]); // Couldn't find any items
            ITEM[0, 5] = Small.Create(null, SIZE[0].X + 230, SIZE[0].Y + 291, Icons.ID.NOTICE, Box_Options.Center, SIZE[0]); // over 100 discarded
            ITEM[0, 6] = Small.Create(null, SIZE[0].X + 232, SIZE[0].Y + 315, Icons.ID.NOTICE, Box_Options.Center, SIZE[0]); // Recieved item
            Cursor_Status |= (Cursor_Status.Hidden | (Cursor_Status.Enabled | Cursor_Status.Static));
        }

        #endregion Methods
    }
}