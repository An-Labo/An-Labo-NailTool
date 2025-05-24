using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public enum AvatarSortOrder {
		[InspectorName("sort_order.default")]
		Default,
		[InspectorName("sort_order.shop_name_asc")]
		ShopNameAsc,
		[InspectorName("sort_order.shop_name_desc")]
		ShopNameDesc,
		[InspectorName("sort_order.avatar_name_asc")]
		AvatarNameAsc,
		[InspectorName("sort_order.avatar_name_desc")]
		AvatarNameDesc,
		[InspectorName("sort_order.newer_asc")]
		NewerAsc,
		[InspectorName("sort_order.newer_desc")]
		NewerDesc,
	}
}