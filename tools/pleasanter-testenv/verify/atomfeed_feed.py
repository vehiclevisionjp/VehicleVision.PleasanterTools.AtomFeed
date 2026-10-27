"""AtomFeed が頼っている、更新日時まわりの Pleasanter の挙動を実機で確かめる。

確かめること:
  1. View.ColumnSorterHash（UpdatedTime の desc / asc）が Get で効くか。PageSize で切ったとき、新しい方が残るか
  2. UpdatedTime への範囲の絞り込み（ColumnFilterHash。"from," の開区間）が効くか
  3. Updator / Creator を KeyValues ＋ Text で表示名に解決できるか

応答は /results へ生のまま残す。解釈は人が読んで _documents/実機検証結果.md へ書く。
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
import api  # noqa: E402

OUT = "/results"


def dump(name, status, body):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ".json"), "w", encoding="utf-8") as f:
        json.dump({"status": status, "body": body}, f, ensure_ascii=False, indent=2)
    print(f"[{status}] {name}")


def get(site_id, view=None, page_size=200):
    body = {"Offset": 0, "PageSize": page_size}
    if view:
        body["View"] = view
    return api.post(f"api/items/{site_id}/Get", body)


def titles(body):
    return [r.get("Title") for r in body.get("Response", {}).get("Data", [])]


def main():
    status, body = api.post("api/items/0/CreateSite", {
        "Title": "記事の検証", "ReferenceType": "Results",
        "SiteSettings": {"TitleColumns": ["Title"], "EditorColumnHash": {"General": ["Title", "Body"]}},
    })
    dump("feed_00_create_site", status, body)
    site_id = body["Id"]

    ids = []
    stamps = []
    for n in range(1, 6):
        status, body = api.post(f"api/items/{site_id}/Create", {"Title": f"記事{n}", "Body": f"本文{n}"})
        ids.append(body["Id"])
        stamps.append(time.strftime("%Y/%m/%d %H:%M:%S", time.gmtime()))  # API キー利用者の TZ（この環境は UTC）の壁時計
        time.sleep(1.2)
    # 1 件目を更新して、更新日時を最新にする（作成順と更新順が食い違う状態にする）
    status, body = api.post(f"api/items/{ids[0]}/Update", {"Body": "更新した"})
    dump("feed_01_update_first", status, body)

    status, body = get(site_id)
    dump("feed_02_default_order", status, body)
    print("  既定の並び:", titles(body))
    for r in body["Response"]["Data"]:
        print("   ", r.get("Title"), r.get("UpdatedTime"), r.get("CreatedTime"))

    status, body = get(site_id, {"ColumnSorterHash": {"UpdatedTime": "desc"}}, page_size=3)
    dump("feed_03_sort_desc_page3", status, body)
    print("  UpdatedTime desc・PageSize 3:", titles(body))

    status, body = get(site_id, {"ColumnSorterHash": {"UpdatedTime": "asc"}}, page_size=3)
    dump("feed_04_sort_asc_page3", status, body)
    print("  UpdatedTime asc・PageSize 3:", titles(body))

    # 3 件目の作成時刻の直後から（記事4・5 と、更新した記事1 が残るはず）
    status, body = get(site_id, {"ColumnFilterHash": {"UpdatedTime": json.dumps([stamps[2] + ","])}})
    dump("feed_05_filter_updated_from", status, body)
    print(f"  UpdatedTime >= {stamps[2]}:", titles(body))

    status, body = get(site_id, {
        "ColumnFilterHash": {"UpdatedTime": json.dumps([stamps[2] + ","])},
        "ColumnSorterHash": {"UpdatedTime": "desc"},
    }, page_size=2)
    dump("feed_06_filter_and_sort", status, body)
    print("  絞り込み＋desc・PageSize 2:", titles(body))

    status, body = get(site_id, {
        "ApiDataType": 1, "ApiColumnKeyDisplayType": 1, "ApiColumnValueDisplayType": 2,
        "GridColumns": ["ResultId", "Updator", "Creator", "Owner"],
    })
    dump("feed_07_resolve_users", status, body)
    print("  Updator / Creator の解決:", [
        {k: r.get(k) for k in ("ResultId", "Updator", "Creator", "Owner")}
        for r in body.get("Response", {}).get("Data", [])[:2]])


if __name__ == "__main__":
    main()
