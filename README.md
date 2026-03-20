# SceneAddMenuInator

Generador de scripts `MenuItem` para Unity a partir de carpetas de escenas, con carga **aditiva** (`OpenSceneMode.Additive`).

## Qué hace

- Escanea una carpeta de escenas (`.unity`) de forma recursiva.
- Permite configurar por escena:
  - inclusión/exclusión en el menú
  - `DisplayName`
  - `Priority Offset` individual
- Colapsa automáticamente submenús con un solo item para evitar niveles innecesarios.
- Genera una clase C# con `MenuItem` para carga aditiva rápida desde el editor.
- Mantiene configuración con `EditorPrefs` entre sesiones.

## Requisitos

- Unity `2022.3` o superior (probado en Unity 6).

## Instalación

### Opción 1: Carpeta en `Assets/Plugins`

Copia este paquete dentro de:

`Assets/Plugins/xavierarpa/SceneAddMenuInator`

### Opción 2: UPM (local o git)

Usa el `package.json` incluido para instalarlo como paquete de Unity.

## Uso

1. Abre `Tools/SceneAddMenuInator`.
2. Selecciona `Scene Folder`.
3. Ajusta `Base Path`, `Menu Name`, `Priority` y `Separator Gap`.
4. Personaliza cada entry (`DisplayName`, offset de prioridad, incluir/excluir).
5. Pulsa `Generate Script`.

## Estructura

- `Editor/SceneAddMenuInatorWindow.cs`
- `Editor/SceneAddMenuInator.Editor.asmdef`

## Notas

- Las escenas siempre se cargan en modo **aditivo** (`OpenSceneMode.Additive`).
- Si un directorio contiene un solo item, el submenú se colapsa y el item sube al nivel padre.
- El orden de la lista de entries se mantiene estable mientras editas campos individuales.
- El reordenado/re-scan se hace al pulsar `Refresh`.
- Al hacer `Refresh`, se limpia el foco/selección de texto para evitar ediciones cruzadas.

# License
[MIT](https://choosealicense.com/licenses/mit/)

<pre>
MIT License

Copyright (c) 2026 Xavier Arpa López Thomas Peter ('xavierarpa')

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
</pre>
