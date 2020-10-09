// Copyright (c) 2006-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LanguageExplorer.Filters;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.Xml;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary />
	internal sealed class ListChoiceComboItem : FilterComboItem
	{
		private int m_hvoList; // root object of list.
		private LcmCache m_cache;
		private IPropertyTable m_propertyTable;
		private FwComboBox m_combo;
		private bool m_fAtomic;
		private XElement m_colSpec;
		/// <summary>non-null for external fields.</summary>
		private Type m_filterType;
		/// <summary />
		private bool m_includeAbbr;
		/// <summary />
		private string m_bestWS;

		private string GetDisplayPropertyName => m_includeAbbr ? "LongName" : "ShortNameTSS";

		/// <summary />
		internal ListChoiceComboItem(ITsString tssName, FilterSortItem fsi, LcmCache cache, IPropertyTable propertyTable, FwComboBox combo, bool fAtomic, Type filterType = null)
			: base(tssName, null, fsi)
		{
			m_colSpec = fsi.Spec;
			if (filterType == null)
			{
				// If the list doesn't exist, m_hvoList (below) will be SpecialHVOValues.kHvoUninitializedObject.
				m_hvoList = BulkEditBar.GetNamedListHvo(cache, fsi.Spec, "list");
				var treeBarHandler = propertyTable.GetValue<IRecordListRepository>(LanguageExplorerConstants.RecordListRepository).ActiveRecordList.MyTreeBarHandler;
				if (treeBarHandler != null)
				{
					m_includeAbbr = treeBarHandler.IncludeAbbreviation;
					m_bestWS = treeBarHandler.BestWritingSystem;
				}
			}
			else
			{
				var mi = filterType.GetMethod("List", BindingFlags.Public | BindingFlags.Static);
				m_hvoList = (int)mi.Invoke(null, new object[] { cache });
			}
			m_cache = cache;
			m_propertyTable = propertyTable;
			m_combo = combo;
			m_fAtomic = fAtomic;
			m_filterType = filterType;
		}

		/// <summary>
		/// Gets or sets the leaf flid.
		/// </summary>
		internal int LeafFlid { get; set; }

		/// <summary>
		/// Invokes this instance.
		/// </summary>
		internal override bool Invoke()
		{
			var labels = GetObjectLabelsForList();
			var oldTargets = new int[0];
			var oldMode = ListMatchOptions.Any;
			if (m_fsi.Filter is ListChoiceFilter)
			{
				oldTargets = ((ListChoiceFilter)m_fsi.Filter).Targets;
				oldMode = ((ListChoiceFilter)m_fsi.Filter).Mode;
			}
			using (var chooser = MakeChooser(labels, oldTargets))
			{
				chooser.Cache = m_cache;
				chooser.SetObjectAndFlid(0, 0);
				chooser.ShowAnyAllNoneButtons(oldMode, m_fAtomic);
				chooser.EnableCtrlClick();
				var res = chooser.ShowDialog(m_fsi.Combo);
				if (System.Windows.Forms.DialogResult.Cancel == res)
				{
					return false;
				}
				var chosenHvos = (chooser.ChosenObjects.Select(obj => obj?.Hvo ?? 0)).ToArray();
				if (chosenHvos.Length == 0)
				{
					return false;
				}
				var filter = MakeFilter(chooser.ListMatchMode, chosenHvos);
				InvokeWithFilter(filter);
				// Enhance JohnT: if there is just one item, maybe we could use its short name somehow?
			}
			return true;
		}

		private ListChoiceFilter MakeFilter(ListMatchOptions matchMode, int[] chosenHvos)
		{
			ListChoiceFilter filter;
			if (m_filterType != null)
			{
				if (m_filterType.IsSubclassOf(typeof(ColumnSpecFilter)))
				{
					var ci = m_filterType.GetConstructor(new[] { typeof(LcmCache), typeof(ListMatchOptions), typeof(int[]), typeof(XElement) });
					filter = (ListChoiceFilter)ci.Invoke(new object[] { m_cache, matchMode, chosenHvos, m_fsi.Spec });
				}
				else
				{
					var ci = m_filterType.GetConstructor(new[] { typeof(LcmCache), typeof(ListMatchOptions), typeof(int[]) });
					filter = (ListChoiceFilter)ci.Invoke(new object[] { m_cache, matchMode, chosenHvos });
				}
			}
			else
			{
				// make a filter that figures path information from the column specs
				filter = new ColumnSpecFilter(m_cache, matchMode, chosenHvos, m_fsi.Spec);
			}
			return filter;
		}

		private IEnumerable<ObjectLabel> GetObjectLabelsForList()
		{
			Debug.Assert(m_hvoList != 0, "Uninitialized List.");
			var list = m_cache.ServiceLocator.GetInstance<ICmPossibilityListRepository>().GetObject(m_hvoList);
			var fShowEmpty = XmlUtils.GetOptionalBooleanAttributeValue(m_colSpec, "canChooseEmpty", false);
			return ObjectLabel.CreateObjectLabels(m_cache, list.PossibilitiesOS, GetDisplayPropertyName, m_bestWS, fShowEmpty);
		}


		/// <summary />
		public void InvokeWithColumnSpecFilter(ListMatchOptions matchMode, List<string> chosenLabels)
		{
			if (m_fsi.Spec == null)
			{
				return; // doesn't have a spec to create ColumnSpecFilter
			}
			var labels = GetObjectLabelsForList();
			var chosenHvos = new List<int>();
			if (chosenLabels.Count > 0)
			{
				FindChosenHvos(labels, chosenLabels, ref chosenHvos);
			}
			var filter = MakeFilter(matchMode, chosenHvos.ToArray());
			InvokeWithFilter(filter);
		}

		private static void FindChosenHvos(IEnumerable<ObjectLabel> labels, List<string> chosenLabels, ref List<int> chosenHvos)
		{
			foreach (var label in labels)
			{
				// go through the labels, and build of list of matching labels.
				if (chosenLabels.Contains(label.DisplayName))
				{
					chosenHvos.Add(label.Object.Hvo);
				}
				// do the same for subitems
				if (label.HaveSubItems)
				{
					FindChosenHvos(label.SubItems, chosenLabels, ref chosenHvos);
				}
			}
		}

		/// <summary />
		private void InvokeWithFilter(ListChoiceFilter filter)
		{
			// This fixes LT-6250.
			filter.MakeUserVisible(true);
			// Todo: do something about persisting and restoring the filter.
			// We can't call base.Invoke because it is designed for FilterBarCellFilters.
			var label = MakeLabel(filter);
			m_fsi.Matcher = null;
			m_fsi.Filter = filter;
			m_combo.SetTssWithoutChangingSelectedIndex(label); // after we set the filter, which may otherwise change it
		}

		private ReallySimpleListChooser MakeChooser(IEnumerable<ObjectLabel> labels, int[] oldTargets)
		{
			var chosenObjs = from hvo in oldTargets select (hvo == 0 ? null : m_cache.ServiceLocator.GetObject(hvo));
			var persistProvider = PersistenceProviderFactory.CreatePersistenceProvider(m_propertyTable);
			return LeafFlid == 0 ? new ReallySimpleListChooser(persistProvider, labels, "Items", m_cache, chosenObjs, m_propertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)) : new LeafChooser(persistProvider, labels, "Items", m_cache, chosenObjs, LeafFlid, m_propertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider));
		}

		private ITsString MakeLabel(ListChoiceFilter filter)
		{
			if (filter.Targets.Length == 1)
			{
				ITsString name;
				if (filter.Targets[0] == 0)
				{
					var empty = new NullObjectLabel();
					name = TsStringUtils.MakeString(empty.DisplayName, m_cache.WritingSystemFactory.UserWs);
				}
				else
				{
					var obj = m_cache.ServiceLocator.GetInstance<ICmObjectRepository>().GetObject(filter.Targets[0]);
					name = obj.ShortNameTSS;
				}
				switch (filter.Mode)
				{
					case ListMatchOptions.None:
						{
							return ComposeLabel(name, XMLViewsStrings.ksNotX);
						}
					case ListMatchOptions.Exact:
						{
							return ComposeLabel(name, XMLViewsStrings.ksOnlyX);
						}
					default: //  appropriate for both Any and All, which mean the same in this case.
						return name;
				}
			}
			string label;
			switch (filter.Mode)
			{
				case ListMatchOptions.All:
					label = XMLViewsStrings.ksAllOf_;
					break;
				case ListMatchOptions.None:
					label = XMLViewsStrings.ksNoneOf_;
					break;
				case ListMatchOptions.Exact:
					label = XMLViewsStrings.ksOnly_;
					break;
				default: // typically Any
					label = XMLViewsStrings.ksAnyOf_;
					break;
			}
			return TsStringUtils.MakeString(label, m_cache.ServiceLocator.WritingSystemManager.UserWs);
		}

		/// <summary>
		/// Compose a label from the ShortNameTSS value formatted with additional text.
		/// </summary>
		private ITsString ComposeLabel(ITsString name, string sFmt)
		{
			var sLabel = string.Format(sFmt, name.Text);
			var tsLabel = TsStringUtils.MakeString(sLabel, m_cache.ServiceLocator.WritingSystemManager.UserWs);
			var ich = sFmt.IndexOf("{0}", StringComparison.Ordinal);
			if (ich < 0)
			{
				return tsLabel;
			}
			var cchName = name.Text?.Length ?? 0;
			if (cchName <= 0)
			{
				return tsLabel;
			}
			var ttp = name.get_Properties(0);
			var bldr = tsLabel.GetBldr();
			bldr.SetProperties(ich, ich + cchName, ttp);
			return bldr.GetString();
		}

		/// <summary>
		/// Determine whether this combo item could have produced the specified filter.
		/// If so, return the string that should be displayed as the value of the combo box
		/// when this filter is active. Otherwise return null.
		/// By default, if the filter is exactly the same, just return your label.
		/// </summary>
		public override ITsString SetFromFilter(IRecordFilter recordFilter, FilterSortItem item)
		{
			return !(recordFilter is ListChoiceFilter filter) ? null : (!filter.CompatibleFilter(m_colSpec) ? null : MakeLabel(filter));
		}
	}
}