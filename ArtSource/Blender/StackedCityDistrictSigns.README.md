# Stacked City District Signs

Original environmental lettering, authored by `Tools/Art/create_stacked_city_district_signs.py` with the existing wall-detail and kit mesh exporters.

The four district plates are 2.4 × 6.8 metres. The twelve smaller shop and neighbourhood plates are 2.6 × 2.2 metres. Unity +Z points out from the wall, +Y is height, the plate is centred in X/Y, and the rear mounting plane is Z = 0. Maximum projection is 0.165 metres. All objects are static meshes without colliders, lights, animation, runtime font or transparent surfaces. Reuse the existing SC_* materials; lettering and plate faces have deliberately separated depth.

The catalogue uses three compact layouts: painted dark shop fascia, light public counter plate with a dark header, and light neighbourhood note with a green margin. Main phrases use high-contrast lettering; small category labels provide secondary detail. The plate outline remains consistent with the architectural kit.

| District model | Number | Main name | Use | Subtitle |
| --- | --- | --- | --- | --- |
| DistrictSignXixia | 03 | 栖霞里 | 住宅街区 | 住在风里 |
| DistrictSignYunting | 16 | 云庭公社 | 公共街区 | 邻里共用 |
| DistrictSignQinglan | 28 | 青岚枢纽 | 交通办公 | 城市换乘 |
| DistrictSignZhexiang | 09 | 折巷老街 | 旧街生活 | 老街新日常 |

| Notice model | Main wording | Secondary wording | Category |
| --- | --- | --- | --- |
| CityNoticeNoodle | 晚风面馆 | 热汤供应 | 街坊小店 |
| CityNoticeBookshop | 云间书屋 | 今日有新书 | 街坊小店 |
| CityNoticeTailor | 衣物修补 | 旧物有新用 | 邻里手作 |
| CityNoticePost | 层间邮局 | 信会准时到 | 邮政服务 |
| CityNoticeGarden | 屋顶菜园 | 请留一片绿 | 共同照料 |
| CityNoticeNightBus | 夜班车站 | 末班也等你 | 夜间服务 |
| CityNoticeWind | 晒被之前 | 请看风向 | 生活提醒 |
| CityNoticeNeighbours | 楼上楼下 | 都是邻居 | 邻里之间 |
| CityNoticeYesterday | 昨日走过 | 今日相逢 | 街角留言 |
| CityNoticeCat | 这层有猫 | 小声经过 | 邻里提醒 |
| CityNoticeBreakfast | 早安蒸铺 | 热气刚刚好 | 街坊小店 |
| CityNoticeLostAndFound | 失物招领 | 也收迷路的伞 | 邻里服务 |

`Assets/Editor/StackedCityWallCatalog.cs` returns model names without extensions. `DistrictModel(theme)` wraps the four themes. `NoticeModel(variant)` visits all twelve notices with a coprime stride of five; both handle negative values and integer boundaries deterministically. The caller must vary its input across buildings and faces. This catalogue does not generate or place runtime objects.

`StackedCityDistrictSigns.manifest.json` contains exact bounds, byte sizes, triangles, material slots, wording and glyph heights. The 16 models contain 44,528 triangles in total. Meshes are merged by material, with at most four renderers per exported plate. The Blender file holds a catalogue inspection arrangement; every exported FBX remains individually centred at its origin. The preview is an asset inspection image, not gameplay acceptance.

Chinese outlines are generated from the installed SimHei font, and number outlines use the existing project EchoRun Sans SC Regular subset (Noto Sans CJK SC, SIL OFL 1.1). Font curves are converted to meshes before export, and unused font datablocks are removed before saving. No font file or texture is embedded or copied into this asset set.
