using System.Collections.Generic;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model {
	public class DBNailShape : DBBase<NailShape> {
		public DBNailShape() : base(MDNailToolDefines.DB_NAIL_SHAPE_FILE_PATH) { }

		public NailShape? FindNailShapeByName(string? name) {
			if (name == null) return null;
			return this._data!.GetValueOrDefault(name, null);
		}
		
	}
}