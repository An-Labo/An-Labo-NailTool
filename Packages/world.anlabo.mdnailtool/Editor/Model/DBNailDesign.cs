using System.Collections.Generic;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model {
	public class DBNailDesign : DBBase<NailDesign> {
		public DBNailDesign() : base(MDNailToolDefines.DB_NAIL_DESIGN_FILE_PATH) { }

		public NailDesign? FindNailDesignByDesignName(string? name) {
			if (name == null) return null;
			return this._data!.GetValueOrDefault(name, null);
		}
	}
}