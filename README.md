# Avant-Garde

[AvantGarde](https://github.com/NeNeppie/AvantGarde) 的台服移植版。在「時尚品鑑」小遊戲視窗上疊加提示分類對應的裝備清單，協助湊出高分搭配。

## 功能

- 於時尚品鑑視窗旁列出各部位提示對應的候選裝備清單。
- 標示裝備是否已擁有（背包、防具庫、裝備中、多功能收納櫃，並透過
  InventoryTools（Allagan Tools）IPC 查詢衣櫥資料）；已擁有的以綠底標示。
- 還沒有的裝備在列上直接顯示取得來源：NPC 商人（顯示是哪一家店）或市場佈告板；
  NPC 商人可按「前往」交給 Lifestream 帶你過去（要不要騎飛行坐騎可在設定切換）。
- 點擊裝備開啟詳細視窗：可穿戴職業、取得方式（購買、成就、副本、討伐戰、
  任務、部族、採集、製作、狩獵、金碟遊樂園、Mog 站、地圖行動、交換、
  無人島等），並可直接試穿或搜尋物品。
- 透過 ItemVendorLocation IPC 顯示販售 NPC 所在位置，點擊可在地圖上標記。

資料來源為社群協作蒐集，各提示分類對應的裝備清單持續由玩家回報更新。

## 台服版的修改

- 新增已擁有綠底標示、列上的取得來源與「前往」按鈕（透過 Lifestream）、點擊商人在地圖上標記。
- 新增設定視窗（前往商人時要不要飛）。
- 介面繁體中文化（用台服官方的「時尚品鑑」）。

## 需要搭配的插件

- 選用：ItemVendorLocation（取得來源與商人位置）、Lifestream（前往商人）、InventoryTools（衣櫥資料）

原作者：[NeNeppie](https://github.com/NeNeppie/AvantGarde)
