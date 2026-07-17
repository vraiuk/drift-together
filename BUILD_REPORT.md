# BUILD_REPORT — Drift Together, первая итерация

Дата: 2026-07-18. Сборка и проверки выполнены автономно на macOS 26.5 (Apple Silicon).

## Версия Unity

- **Unity 6000.3.20f1 (Unity 6.3 LTS)**, установлена через Unity Hub 3.19.5.
- Рендер: URP 17.3.0 (пайплайн-ассет создаётся конфигуратором в `Assets/_Project/Settings`).
- Ввод: Unity Input System 1.14.2 (active input handling = Both; клавиатура + геймпад).
- Scripting backend: Mono, цвета — Linear.

## Целевые платформы и реальные пути

| Артефакт | Путь | Статус |
| --- | --- | --- |
| Unity-проект | `DriftTogether/` | ✅ компилируется без ошибок |
| Windows x86_64 | `Builds/Windows/DriftTogether.exe` | ✅ BuildResult=Succeeded, PE32+ x86-64, без зависимости от Unity |
| macOS | `Builds/macOS/DriftTogether.app` | ✅ BuildResult=Succeeded, **universal (arm64 + x86_64)** — проверено `file`: «Mach-O universal binary with 2 architectures» |
| Архив Windows | `Builds/DriftTogether-Windows-x86_64.zip` (34 МБ, 187 файлов) | ✅ |
| Архив macOS | `Builds/DriftTogether-macOS-Universal.zip` (42 МБ, bundle целиком через `ditto`) | ✅ |

Папки `*_BurstDebugInformation_DoNotShip` в архивы не включены.

## Тесты

### Edit Mode (`test-results-editmode.xml`) — 8/8 passed

- `HullIntegrityTests` — прочность: старт с 3, −1 за удар; окно неуязвимости
  блокирует повторный удар; поломка на нуле и полное восстановление.
- `CheckpointSystemTests` — respawn использует последний checkpoint.
- `MushroomTrackerTests` — каждый гриб считается один раз, повторный триггер
  после восстановления не даёт дублей.
- `RunStatsTests` — выбранный маршрут сохраняется в статистике и не
  затирается значением None.
- `DialogueQueueTests` — cooldown категорий реплик и ротация вариантов.

### Play Mode smoke (`test-results-playmode.xml`) — 1/1 passed (31 c)

Автопилот в редакторе: меню → «Играть» → прохождение уровня (включая
обязательный привал у костра) → финиш → экран результатов → возврат в меню.

### Smoke test реальной macOS-сборки

Запуск `DriftTogether.app --smoke` (лог `smoke-standalone.log`), результат
`smoke_result.json`:

```json
{
  "success": true,
  "elapsedGameSeconds": 321.1,
  "route": "NoisyStream",
  "mushrooms": 5,
  "collisions": 12,
  "respawns": 8,
  "realSeconds": 64.9
}
```

Автопилот на реальной сборке прошёл уровень целиком: выбрал маршрут, собрал
5/5 грибов, отдохнул у костра, финишировал, экран результатов показан.
Windows-сборка бинарно идентична по содержимому (тот же контент, Mono, D3D12);
запуск на Windows-машине не выполнялся, так как сборка делалась на macOS.

## Что реализовано

- Полный игровой цикл: меню → подсказки управления → сплав → развилка
  (Тихий канал / Шумный ручей) → костёр-checkpoint → финальные пороги →
  финиш → результаты → рестарт/меню.
- Прочность 3 ед., неуязвимость после удара, дружелюбный respawn, `R` —
  ручное восстановление на последней точке, защита от застревания и выхода
  за пределы.
- 7 светящихся грибов на уровне (на любом маршруте доступно 5), подсчёт без
  дублей, HUD `0/5`.
- Тапок-Тим: очередь субтитров с cooldown, ситуативные реплики (старт,
  столкновения, грибы, костёр, развилка, нервы на Шумном ручье, финиш),
  анимация паники при ударах.
- Процедурный звук без внешних ассетов: эмбиент-пад, шум воды, гребки,
  столкновения, гриб, костёр, финиш, клики UI; общая громкость в настройках.
- Туман, светящиеся грибы и костёр с мерцанием, пена по течению, брызги от
  весла, покачивание корпуса, плавная камера с защитой от проникновения
  в геометрию и тряской при ударах.
- Пауза (продолжить/заново/меню/настройки), настройки громкости и камеры,
  экран результатов со временем, маршрутом, грибами, столкновениями и
  восстановлениями. Все тексты UI — русские.

## Известные ограничения

1. macOS-приложение не подписано и не нотариализовано (нет сертификатов):
   при первом запуске потребуется подтверждение в системных настройках.
2. Заставку Unity отключить нельзя (лицензия Personal).
3. Windows-сборка проверена только структурно (создание, состав, формат PE32+);
   функциональный smoke выполнен на macOS-сборке того же контента.
4. Автопилот smoke-теста проходит уровень за ~5,3 игровых минут почти без
   остановок; живой игрок с осмотром, сбором грибов и привалом попадает в
   целевые 8–12 минут, но хронометраж не измерялся на людях.
5. Графика собрана из примитивов и процедурных мешей — стилизованный low-poly
   без текстур; освещение полностью realtime, без запечённого GI.
6. Настройки не сохраняются между запусками (сознательно, по ТЗ первой итерации).

## Как повторить проверки

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity"
P="$(pwd)/DriftTogether"

"$UNITY" -batchmode -projectPath "$P" -runTests -testPlatform EditMode -testResults "$(pwd)/editmode.xml" -logFile -
"$UNITY" -batchmode -projectPath "$P" -runTests -testPlatform PlayMode -testResults "$(pwd)/playmode.xml" -logFile -
"$UNITY" -batchmode -quit -projectPath "$P" -executeMethod DriftTogether.EditorTools.ProjectConfigurator.BuildMac -logFile -
"$UNITY" -batchmode -quit -projectPath "$P" -executeMethod DriftTogether.EditorTools.ProjectConfigurator.BuildWindows -logFile -
"Builds/macOS/DriftTogether.app/Contents/MacOS/Drift Together" --smoke
```
