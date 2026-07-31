using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SceneXRay.Editor.UI
{
    public static class XRayLocalization
    {
        public enum Language { English, Ukrainian }

        private static readonly Dictionary<Language, Dictionary<string, string>> _translations = new();
        private static Language _currentLanguage = Language.English;

        /// <summary>Raised after <see cref="SetLanguage"/> so open windows can refresh labels.</summary>
        public static event Action LanguageChanged;

        static XRayLocalization()
        {
            // A single duplicated key used to throw out of the type initializer, which took
            // every SceneXRay window down with it — degrade to raw keys instead.
            try
            {
                LoadTranslations();
            }
            catch (Exception ex)
            {
                Debug.LogError($"SceneXRay: localization table is broken ({ex.Message}). " +
                               "Falling back to raw keys — look for a duplicated entry.");
            }

            if (!_translations.ContainsKey(Language.English))
                _translations[Language.English] = new Dictionary<string, string>();
            if (!_translations.ContainsKey(Language.Ukrainian))
                _translations[Language.Ukrainian] = new Dictionary<string, string>();

            _currentLanguage = (Language)EditorPrefs.GetInt("SceneXRay_Language", 0);
            if (!_translations.ContainsKey(_currentLanguage))
                _currentLanguage = Language.English;
        }

        private static void LoadTranslations()
        {
            _translations[Language.English] = new Dictionary<string, string>
            {
                {"refresh", "Refresh"},
                {"health_score", "Health Score"},
                {"missing_refs", "Missing References"},
                {"cycles_detected", "Cycles Detected"},
                {"export", "Export"},
                {"settings", "Settings"},
                {"fix_missing", "Fix Missing"},
                {"bookmarks", "Bookmarks"},
                {"global_search", "Global Search"},
                {"quality_gates", "Quality Gates"},
                {"live_mode", "Live Mode"},
                {"snapshot", "Snapshot"},
                {"compare", "Compare"},
                {"search", "Search"},
                {"component", "Component"},
                {"link_type", "Link Type"},
                {"depth", "Depth"},
                {"selected_only", "Selected Only"},
                {"prefabs", "Prefabs"},
                {"cache", "Cache"},
                {"follow", "Follow"},
                {"minimap", "MiniMap"},
                {"more", "More…"},
                {"clear_cache", "Clear Cache"},
                {"save_snapshot", "Save Snapshot"},
                {"compare_snapshots", "Compare Snapshots…"},
                {"scene_diff", "Scene Diff"},
                {"tutorial", "Tutorial"},
                {"search_placeholder", "Search..."},
                {"all_components", "All Components"},
                {"all_links", "All Links"},
                {"direct", "Direct"},
                {"unityevent", "UnityEvent"},
                {"missing", "Missing"},
                {"asset", "Asset"},
                {"mode", "Mode:"},
                {"mode_all", "All"},
                {"mode_scene", "Scene"},
                {"mode_project", "Project"},
                {"results_found", "{0} results found"},
                {"empty_search_title", "Find references to any GameObject"},
                {"empty_search_hint", "Type a name, drag from Hierarchy, or select from suggestions"},
                {"drop_here", "Drop GameObject Here"},
                {"tt_refresh", "Rescan the scene and rebuild the graph"},
                {"tt_search", "Filter nodes by name"},
                {"tt_component", "Show only nodes that have this component (Collider = any Collider, Script = any MonoBehaviour)"},
                {"tt_link_type", "Show only edges of this link type"},
                {"tt_depth", "Max hierarchy depth (-1 = all)"},
                {"tt_follow", "Automatically focus the graph on whatever GameObject is selected in the Hierarchy/Scene"},
                {"tt_prefabs", "Also include dependency links from prefab assets used by instances in the loaded scenes"},
                {"tt_more", "Cache, bookmarks, tutorial…"},
                {"tt_global_search", "Open Global Search"},
                {"bm_add", "Add Selection"},
                {"bm_toggle", "Bookmark"},
                {"bm_remove", "Remove"},
                {"bm_clear", "Clear"},
                {"bm_clear_confirm", "Remove all bookmarks?"},
                {"bm_yes", "Yes"},
                {"bm_no", "No"},
                {"bm_count", "{0} bookmarks"},
                {"bm_empty_title", "No bookmarks yet"},
                {"bm_empty_hint", "Select a GameObject and press Ctrl+Shift+Alt+J\nto pin it for quick jump later."},
                {"bm_graph", "Graph"},
                {"bm_missing", "Not in loaded scenes"},
                {"bm_resolve_fail", "'{0}' not found — load its scene first."},
                {"bm_removed", "Removed: {0}"},
                {"graph_back", "← Back"},
                {"graph_forward", "Fwd →"},
                {"graph_back_tt", "Previous view (Backspace / Alt+←). Restores camera + full-graph layout."},
                {"graph_forward_tt", "Next view (Alt+→)"},
                {"graph_prev_page", "Previous page"},
                {"graph_next_page", "Next page"},
                {"graph_radius_tt", "Neighborhood radius (= expand, − shrink / back)"},
                {"graph_dir_both", "Both"},
                {"graph_dir_out", "Out"},
                {"graph_dir_in", "In"},
                {"graph_dir_tt", "Both = undirected · Out = dependencies · In = referenced by"},
                {"graph_focus_selected", "Focus Selected"},
                {"graph_focus_selected_tt", "Drill into selected node (Enter). Double-click a node does the same."},
                {"graph_show_all", "Show All"},
                {"graph_show_all_tt", "Exit focus to full graph (keeps layout; Esc steps back first)"},
                {"graph_empty", "No dependencies to show.\nPress Refresh, then double-click a node or press Enter to focus."},
                {"graph_page_focus", "Focus"},
                {"graph_page", "Page {0}/{1}"},
                {"graph_focus_status", "Focus: {0} · r={1} · {2} · {3} nodes"},
                {"graph_focus_missing", "'{0}' has no links in the graph"},
                {"graph_select_first", "Select a node in the graph or Hierarchy first"},
                {"graph_focus_this", "Focus On This"},
                {"graph_expand_radius", "Expand Radius (=)"},
                {"graph_shrink_radius", "Shrink Radius (−)"},
                {"graph_nav_back", "Back"},
                {"graph_select_hierarchy", "Select in Hierarchy"},
                {"graph_select_project", "Select in Project"},
                {"graph_view_refs", "View References"},
                {"graph_find_missing", "Find Missing"},
                {"graph_crumb_all", "All"},
                {"graph_crumb_meta", " (r={0}, {1})"},
                {"graph_legend", "Enter focus · Backspace back · Esc up · ←↑↓→ move · = / − radius · A fit · Ctrl+wheel zoom · MMB pan"},
                {"graph_counts", "{0}/{1} nodes · {2} links"},
                {"graph_zoom_in", "Zoom in (Ctrl+=)"},
                {"graph_zoom_out", "Zoom out (Ctrl+−)"},
                {"graph_zoom_reset", "Reset zoom to 100% (0)"},
                {"graph_fit", "Fit to window (A)"},
                {"graph_layout", "Layout"},
                {"graph_layout_tt", "How nodes are placed. Auto = Tree for the whole graph, Radial while focused."},
                {"graph_layout_auto", "Auto"},
                {"graph_layout_tree", "Tree"},
                {"graph_layout_force", "Force"},
                {"graph_layout_radial", "Radial"},
                {"graph_relayout", "Re-layout now"},
                {"graph_layout_save", "Save my layout"},
                {"graph_layout_restore", "Restore my layout"},
                {"graph_layout_clear", "Delete saved layout"},
                {"graph_layout_autorestore", "Auto-restore on open"},
                {"graph_layout_saved", "Layout saved — {0} nodes for '{1}'"},
                {"graph_layout_restored", "Layout restored — {0} nodes"},
                {"graph_layout_none", "No saved layout for this scene"},
                {"graph_layout_cleared", "Saved layout deleted"},
                {"graph_layout_empty", "Nothing to save — the graph is empty"},
                {"graph_undo", "Undo move"},
                {"graph_redo", "Redo move"},
                {"graph_undo_empty", "Nothing to undo"},
                {"graph_undo_done", "Undo"},
                {"graph_redo_done", "Redo"},
                {"graph_center_here", "Center camera here"},
                {"graph_subtitle_scene", "Scene root"},
                {"graph_subtitle_asset", "Asset"},
                {"graph_subtitle_missing", "Missing reference"},
                {"report_nothing", "Nothing to show"},
                {"report_click_hint", "Click to select and ping"},
                {"report_no_missing", "No missing references"},
                {"report_no_cycles", "No dependency cycles"},
                {"report_no_god", "No object has more than 10 links"},
                {"report_god_objects", "High connectivity (>10)"},
                {"report_god_hint", "Refactor candidate"},
                {"report_analysis", "Analysis"},
                {"report_analysis_summary", "{0} selected node(s)"},
                {"report_selected_nodes", "Selected nodes"},
                {"report_node_stats", "{0} links · {1} out · {2} in · {3} components · {4} missing"},
                {"report_missing_summary", "'{0}' — {1} missing reference(s)"},
                {"fix_missing_result", "Fixed {0} of {1} reference(s)"},
                {"fix_missing_hint", "Broken object references in the loaded scenes. Select a row to ping its object, then fix by name."},
                {"fix_missing_none", "No missing references"},
                {"fix_missing_none_hint", "Every reference in the loaded scenes resolves."},
                {"diff_hint", "Compare the dependency links of two scene assets without opening them."},
                {"diff_pick_scenes", "Please select both scenes."},
                {"diff_only_a", "Only in A"},
                {"diff_only_b", "Only in B"},
                {"diff_common", "Common"},
                {"tutorial_step", "Step {0} of {1}"},
                {"tutorial_finish", "Finish"},
                {"next", "Next"},
                {"skip", "Skip"},
            };

            _translations[Language.Ukrainian] = new Dictionary<string, string>
            {
                {"refresh", "Оновити"},
                {"health_score", "Оцінка здоров'я"},
                {"missing_refs", "Відсутні посилання"},
                {"cycles_detected", "Знайдено циклів"},
                {"export", "Експорт"},
                {"settings", "Налаштування"},
                {"fix_missing", "Виправити відсутні"},
                {"bookmarks", "Закладки"},
                {"global_search", "Глобальний пошук"},
                {"quality_gates", "Контроль якості"},
                {"live_mode", "Live-режим"},
                {"snapshot", "Снапшот"},
                {"compare", "Порівняти"},
                {"search", "Пошук"},
                {"component", "Компонент"},
                {"link_type", "Тип зв'язку"},
                {"depth", "Глибина"},
                {"selected_only", "Лише вибрані"},
                {"prefabs", "Префаби"},
                {"cache", "Кеш"},
                {"follow", "Слідкувати"},
                {"minimap", "Мінімапа"},
                {"more", "Ще…"},
                {"clear_cache", "Очистити кеш"},
                {"save_snapshot", "Зберегти снапшот"},
                {"compare_snapshots", "Порівняти снапшоти…"},
                {"scene_diff", "Різниця сцен"},
                {"tutorial", "Туторіал"},
                {"search_placeholder", "Пошук..."},
                {"all_components", "Усі компоненти"},
                {"all_links", "Усі зв'язки"},
                {"direct", "Прямий"},
                {"unityevent", "UnityEvent"},
                {"missing", "Відсутній"},
                {"asset", "Асет"},
                {"mode", "Режим:"},
                {"mode_all", "Усе"},
                {"mode_scene", "Сцена"},
                {"mode_project", "Проєкт"},
                {"results_found", "Знайдено результатів: {0}"},
                {"empty_search_title", "Знайдіть посилання на будь-який GameObject"},
                {"empty_search_hint", "Введіть ім'я, перетягніть з Hierarchy або виберіть з підказок"},
                {"drop_here", "Киньте GameObject сюди"},
                {"tt_refresh", "Пересканувати сцену та перебудувати граф"},
                {"tt_search", "Фільтр вузлів за ім'ям"},
                {"tt_component", "Показати лише вузли з цим компонентом (Collider = будь-який Collider, Script = будь-який MonoBehaviour)"},
                {"tt_link_type", "Показати лише ребра цього типу"},
                {"tt_depth", "Макс. глибина ієрархії (-1 = усі)"},
                {"tt_follow", "Автофокус графа на вибраному в Hierarchy/Scene об'єкті"},
                {"tt_prefabs", "Також включити залежності префабів, інстанси яких є в завантажених сценах"},
                {"tt_more", "Кеш, закладки, туторіал…"},
                {"tt_global_search", "Відкрити глобальний пошук"},
                {"bm_add", "Додати вибране"},
                {"bm_toggle", "Закладка"},
                {"bm_remove", "Прибрати"},
                {"bm_clear", "Очистити"},
                {"bm_clear_confirm", "Прибрати всі закладки?"},
                {"bm_yes", "Так"},
                {"bm_no", "Ні"},
                {"bm_count", "Закладок: {0}"},
                {"bm_empty_title", "Поки немає закладок"},
                {"bm_empty_hint", "Вибери GameObject і натисни Ctrl+Shift+Alt+J,\nщоб швидко повертатися до нього."},
                {"bm_graph", "Граф"},
                {"bm_missing", "Сцена не завантажена"},
                {"bm_resolve_fail", "'{0}' не знайдено — спочатку відкрий сцену."},
                {"bm_removed", "Прибрано: {0}"},
                {"graph_back", "← Назад"},
                {"graph_forward", "Вперед →"},
                {"graph_back_tt", "Попередній вид (Backspace / Alt+←). Відновлює камеру і layout повного графа."},
                {"graph_forward_tt", "Наступний вид (Alt+→)"},
                {"graph_prev_page", "Попередня сторінка"},
                {"graph_next_page", "Наступна сторінка"},
                {"graph_radius_tt", "Радіус сусідства (= ширше, − вужче / назад)"},
                {"graph_dir_both", "Обидва"},
                {"graph_dir_out", "Вихід"},
                {"graph_dir_in", "Вхід"},
                {"graph_dir_tt", "Обидва = без напрямку · Вихід = залежності · Вхід = хто посилається"},
                {"graph_focus_selected", "Фокус на вибране"},
                {"graph_focus_selected_tt", "Увійти в ноду (Enter). Подвійний клік — те саме."},
                {"graph_show_all", "Показати все"},
                {"graph_show_all_tt", "Вийти з фокусу до повного графа (layout лишається; Esc спочатку крок назад)"},
                {"graph_empty", "Немає залежностей.\nRefresh, потім double-click або Enter для фокусу."},
                {"graph_page_focus", "Фокус"},
                {"graph_page", "Стор. {0}/{1}"},
                {"graph_focus_status", "Фокус: {0} · r={1} · {2} · {3} нод"},
                {"graph_focus_missing", "'{0}' немає зв'язків у графі"},
                {"graph_select_first", "Спочатку вибери ноду в графі або Hierarchy"},
                {"graph_focus_this", "Фокус на це"},
                {"graph_expand_radius", "Розширити радіус (=)"},
                {"graph_shrink_radius", "Звузити радіус (−)"},
                {"graph_nav_back", "Назад"},
                {"graph_select_hierarchy", "Вибрати в Hierarchy"},
                {"graph_select_project", "Вибрати в Project"},
                {"graph_view_refs", "Переглянути референси"},
                {"graph_find_missing", "Знайти missing"},
                {"graph_crumb_all", "Усе"},
                {"graph_crumb_meta", " (r={0}, {1})"},
                {"graph_legend", "Enter фокус · Backspace назад · Esc вгору · ←↑↓→ перехід · = / − радіус · A вмістити · Ctrl+колесо зум · СКМ панорама"},
                {"graph_counts", "{0}/{1} вузлів · {2} зв'язків"},
                {"graph_zoom_in", "Наблизити (Ctrl+=)"},
                {"graph_zoom_out", "Віддалити (Ctrl+−)"},
                {"graph_zoom_reset", "Скинути зум до 100% (0)"},
                {"graph_fit", "Вмістити у вікно (A)"},
                {"graph_layout", "Розкладка"},
                {"graph_layout_tt", "Як розставляються вузли. Auto = Tree для всього графа, Radial у фокусі."},
                {"graph_layout_auto", "Авто"},
                {"graph_layout_tree", "Дерево"},
                {"graph_layout_force", "Сила"},
                {"graph_layout_radial", "Радіальна"},
                {"graph_relayout", "Перерозкласти зараз"},
                {"graph_layout_save", "Зберегти мою розкладку"},
                {"graph_layout_restore", "Відновити мою розкладку"},
                {"graph_layout_clear", "Видалити збережену розкладку"},
                {"graph_layout_autorestore", "Відновлювати при відкритті"},
                {"graph_layout_saved", "Розкладку збережено — {0} вузлів для «{1}»"},
                {"graph_layout_restored", "Розкладку відновлено — {0} вузлів"},
                {"graph_layout_none", "Для цієї сцени немає збереженої розкладки"},
                {"graph_layout_cleared", "Збережену розкладку видалено"},
                {"graph_layout_empty", "Нема чого зберігати — граф порожній"},
                {"graph_undo", "Скасувати переміщення"},
                {"graph_redo", "Повернути переміщення"},
                {"graph_undo_empty", "Нема чого скасовувати"},
                {"graph_undo_done", "Скасовано"},
                {"graph_redo_done", "Повернено"},
                {"graph_center_here", "Камеру сюди"},
                {"graph_subtitle_scene", "Корінь сцени"},
                {"graph_subtitle_asset", "Асет"},
                {"graph_subtitle_missing", "Відсутнє посилання"},
                {"report_nothing", "Нема чого показати"},
                {"report_click_hint", "Клік — вибрати й підсвітити"},
                {"report_no_missing", "Відсутніх посилань немає"},
                {"report_no_cycles", "Циклів залежностей немає"},
                {"report_no_god", "Жоден об'єкт не має понад 10 зв'язків"},
                {"report_god_objects", "Висока зв'язність (>10)"},
                {"report_god_hint", "Кандидат на рефакторинг"},
                {"report_analysis", "Аналіз"},
                {"report_analysis_summary", "Вибрано вузлів: {0}"},
                {"report_selected_nodes", "Вибрані вузли"},
                {"report_node_stats", "{0} зв'язків · {1} вих. · {2} вх. · {3} компонентів · {4} missing"},
                {"report_missing_summary", "«{0}» — відсутніх посилань: {1}"},
                {"fix_missing_result", "Виправлено {0} з {1} посилань"},
                {"fix_missing_hint", "Побиті посилання на об'єкти у завантажених сценах. Клік по рядку підсвітить об'єкт, далі — фікс за іменем."},
                {"fix_missing_none", "Відсутніх посилань немає"},
                {"fix_missing_none_hint", "Усі посилання у завантажених сценах на місці."},
                {"diff_hint", "Порівняти залежності двох сцен, не відкриваючи їх."},
                {"diff_pick_scenes", "Вибери обидві сцени."},
                {"diff_only_a", "Лише в A"},
                {"diff_only_b", "Лише в B"},
                {"diff_common", "Спільні"},
                {"tutorial_step", "Крок {0} з {1}"},
                {"tutorial_finish", "Готово"},
                {"next", "Далі"},
                {"skip", "Пропустити"},
            };
        }

        public static string GetText(string key)
        {
            if (_translations.TryGetValue(_currentLanguage, out var table) &&
                table.TryGetValue(key, out string value))
                return value;
            if (_translations.TryGetValue(Language.English, out var fallback))
                return fallback.GetValueOrDefault(key, key);
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string pattern = GetText(key);
            try { return string.Format(pattern, args); }
            catch (FormatException) { return pattern; } // malformed placeholder must not kill the UI
        }

        public static void SetLanguage(Language lang)
        {
            if (_currentLanguage == lang) return;
            _currentLanguage = lang;
            EditorPrefs.SetInt("SceneXRay_Language", (int)lang);
            LanguageChanged?.Invoke();
        }

        public static Language CurrentLanguage => _currentLanguage;
    }
}
