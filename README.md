# TaskbarMin

![Build](https://github.com/TU-USUARIO/TaskbarMin/actions/workflows/build.yml/badge.svg)
![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4.svg)

Aplicación minimalista y ligera que deja la **barra de tareas de Windows 11 transparente o con desenfoque**. Se ejecuta en segundo plano desde la bandeja del sistema y arranca con Windows.

<!-- Añade aquí una captura: ![Captura](docs/screenshot.png) -->

## Características

- Cuatro estilos: **Transparente**, **Desenfoque**, **Acrílico** y **Normal**.
- Opacidad ajustable (0–80 %) para desenfoque y acrílico.
- Soporte para **varios monitores**.
- **Inicio automático** con Windows (opcional, desde el instalador o el menú).
- Consumo mínimo: se reaplica por eventos, no por sondeo agresivo.
- Una sola instancia; al salir devuelve la barra de tareas a su estado original.
- Recuerda tu configuración entre sesiones.

## Instalación

1. Ve a [**Releases**](../../releases/latest) y descarga `TaskbarMin-Setup-X.Y.Z.exe`.
2. Ejecuta el instalador y sigue el asistente (no requiere permisos de administrador).
3. Marca *«Iniciar TaskbarMin automáticamente con Windows»* si quieres que arranque siempre.

> **Aviso de SmartScreen:** el instalador no está firmado digitalmente, así que Windows puede mostrar «Windows protegió su PC». Pulsa **Más información → Ejecutar de todos modos**.

## Uso

TaskbarMin no tiene ventana: busca su icono en la bandeja del sistema (junto al reloj; puede estar en «Mostrar iconos ocultos») y haz **clic derecho** para cambiar el estilo, la opacidad, el inicio automático o salir.

## Compilar desde el código

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download) y, opcionalmente, [Inno Setup 6](https://jrsoftware.org/isinfo.php) para generar el instalador.

```powershell
git clone https://github.com/TU-USUARIO/TaskbarMin.git
cd TaskbarMin
.\scripts\build.ps1 -Version 1.0.0
```

- El `.exe` autocontenido queda en `publish\`.
- El instalador queda en `dist\`.

## Estructura del proyecto

```
TaskbarMin/
├── .github/workflows/build.yml   # CI: compila, genera el instalador y publica Releases
├── src/TaskbarMin/               # Código fuente (C# / .NET 10 / Windows Forms)
├── installer/TaskbarMin.iss      # Script del instalador (Inno Setup)
├── scripts/build.ps1             # Compilación local
├── docs/                         # Capturas y documentación
├── CHANGELOG.md
├── LICENSE
└── README.md
```

## Cómo funciona

Usa la API no documentada de Windows `SetWindowCompositionAttribute` sobre las ventanas de la barra de tareas (`Shell_TrayWnd` y `Shell_SecondaryTrayWnd`), la misma técnica que utilizan otras herramientas similares. El estilo se reaplica cuando cambia la ventana en primer plano, cuando `explorer.exe` se reinicia y con un temporizador de seguridad de 1 s, porque Windows a veces lo restablece.

## Limitaciones conocidas

- Al depender de una API no documentada, una actualización de Windows podría cambiar su comportamiento.
- Si el proceso se termina a la fuerza (Administrador de tareas), la barra puede quedarse con el último estilo hasta reiniciar el Explorador. Usa **Salir** en el menú de la bandeja.
- Todavía no hay estilos distintos para ventana maximizada ni colores personalizados.

## Hoja de ruta

- [ ] Estilo distinto cuando hay una ventana maximizada
- [ ] Colores personalizados
- [ ] Icono propio y firma de código

## Contribuir

Las *issues* y *pull requests* son bienvenidas. Para cambios grandes, abre antes una *issue* para comentarlo.

## Licencia

[MIT](LICENSE)
