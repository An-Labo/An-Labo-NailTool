using System.Collections.Generic;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model {
	public class DBShop : DBBase<Shop> {
		
		public DBShop() : base(MDNailToolDefines.DB_SHOP_FILE_PATH) {}

		public Shop? FindShopByName(string? name) {
			if (name == null) return null;
			return this._data!.GetValueOrDefault(name, null);
		}
		
		
	}
}