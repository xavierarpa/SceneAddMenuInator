# Changelog

All notable changes to this project will be documented in this file.

## [0.1.0] - 2026-03-20

### Added
- Ventana `Tools/SceneAddMenuInator` para generar scripts de `MenuItem` de carga aditiva de escenas.
- Escaneo recursivo de escenas (`.unity`) por carpeta fuente.
- Configuración global: base path, nombre de menú, prioridad base, separadores.
- Configuración individual por escena: `DisplayName`, `Priority Offset`, incluir/excluir.
- Preview del árbol de menú y ruta de salida del script.
- Generación de script C# con helper `LoadSceneAdditive` usando `EditorSceneManager.OpenScene` en modo aditivo.
- Colapso automático de submenús con un solo item (elimina niveles innecesarios).
- Persistencia de settings usando `EditorPrefs`.
