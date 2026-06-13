using System.Collections.Generic;
using System.Linq;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model {
	public class DBNailDesign : DBBase<NailDesign> {
		public DBNailDesign() : base(MDNailToolDefines.DB_NAIL_DESIGN_FILE_PATH) { }

		public NailDesign? FindNailDesignByDesignName(string? name) {
			if (name == null) return null;
			// 完全一致を最優先. ヒットしなければ大小無視 fallback (Legacy 商品名と Resource フォルダ名の表記揺れ吸収).
			NailDesign? hit = this._data!.GetValueOrDefault(name, null);
			if (hit != null) return hit;
			foreach (var kv in this._data) {
				if (string.Equals(kv.Key, name, System.StringComparison.OrdinalIgnoreCase)) return kv.Value;
			}
			return null;
		}

		/// <summary>指定デザインを親として参照している子バリ一覧を返す</summary>
		public IReadOnlyList<NailDesign> FindChildVariants(string parentDesignName) {
			return this._data.Values
				.Where(d => !string.IsNullOrEmpty(d.ParentVariant)
				         && string.Equals(d.ParentVariant, parentDesignName, System.StringComparison.OrdinalIgnoreCase))
				.OrderBy(d => d.Id)
				.ToList();
		}

		/// <summary>指定デザインが子バリを持つ親かどうか</summary>
		public bool HasChildVariants(string designName) {
			return this._data.Values.Any(d =>
				string.Equals(d.ParentVariant, designName, System.StringComparison.OrdinalIgnoreCase));
		}
	}
}