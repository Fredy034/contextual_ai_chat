# contextual_ai_chat

Proyecto de chat inteligente y gestor documental con IA contextual.

## Descripción

Esta solución integra un sistema de chat web minimalista y profesional, capaz de responder preguntas sobre documentos subidos y realizar búsquedas en la web, utilizando modelos de lenguaje y embeddings.

## Módulos principales

- **CaseChatbotNLP**: Frontend web con chat, subida y gestión de archivos, visualización y descarga de documentos. Incluye UI responsiva, selector de tipo de consulta, y estilos diferenciados por tipo de archivo.
- **TextSimilarityApi**: Backend .NET para procesamiento de embeddings, almacenamiento en SQLite, extracción de texto, y endpoints para búsqueda, subida, descarga y listado de documentos.

## Funcionalidades

- Chat con alineación de mensajes y formato markdown.
- Selector de tipo de consulta (tabla o lenguaje natural).
- Subida de archivos con indicador de carga y nombre visible.
- Visualización y descarga de documentos (PDF, TXT, DOCX, XLSX, CSV, imágenes, Videos, etc.).
- Diferenciación visual de documentos por extensión.
- Backend con endpoints para embeddings, búsqueda semántica y gestión de archivos.
- Integración con Azure OpenAI para embeddings y respuestas contextuales.

## Estructura

- `CaseChatbotNLP/wwwroot/`: Archivos estáticos, frontend, estilos y scripts.
- `TextSimilarityApi/`: API backend, servicios, controladores y base de datos embeddings.db.

## Uso

1. Ejecuta el backend .NET (`TextSimilarityApi`).
2. Abre el frontend (`CaseChatbotNLP/wwwroot/index.html`).
3. Sube documentos, consulta por chat y gestiona archivos desde la interfaz web.

## Requisitos

- .NET 8
- SQLite
- Azure OpenAI (configuración en `appsettings.json`)

## Estado actual

- UI moderna y responsiva.
- Descarga y vista previa de documentos funcionales.
- Embeddings y búsqueda semántica operativos.
- Código modular y fácil de mantener.

## CaseChatbotNLP

### Descripción General

CaseChatbotNLP es una aplicación web profesional para la gestión de casos y consulta inteligente mediante chat, con integración de IA contextual y procesamiento de documentos. El frontend está construido en HTML, CSS y JavaScript modular, y se conecta con servicios backend para procesamiento de lenguaje natural, embeddings y gestión documental.

### Arquitectura y Componentes

- **Frontend Web**: UI moderna y responsiva, con chat, subida de archivos, visualización y descarga de documentos.
- **Integración con Backend**: Comunicación vía API REST con servicios .NET para procesamiento de consultas, embeddings y manejo de archivos.
- **Modularidad**: Scripts organizados por funcionalidad (`chat.js`, `upload.js`, `document.js`, etc.), facilitando mantenimiento y escalabilidad.

### Principales Módulos

- **Chat Inteligente (`chat.js`)**:

  - Renderizado de mensajes con formato markdown y acciones (copiar, escuchar, reintentar, compartir).
  - Historial de conversación y manejo de contexto.
  - Integración con SpeechSynthesis API para mensajes de voz.
  - Soporte para reintentos agrupados y tooltips personalizados.

- **Gestión de Archivos (`upload.js`, `document.js`, `download.js`)**:

  - Subida de archivos con indicador de progreso y validación de extensiones.
  - Listado, filtrado y descarga de documentos.
  - Diferenciación visual por tipo de archivo (PDF, DOCX, imágenes, videos, etc.).
  - Subidas globales y por sesión, con control de `sessionId` según contexto.

- **UI y Navegación (`index.html`, `buscar.html`, `buscarweb.html`, `upload.html`)**:

  - Navegación entre módulos mediante barra superior y menú hamburguesa.
  - Formularios para consulta, subida y registro de errores.
  - Responsive design y estilos personalizados (`styles.css`).

- **Utilidades y Helpers (`utils.js`, `fileUI.js`, `nav.js`, `voice.js`)**:
  - Funciones para manejo seguro de HTML, iconos por extensión, tooltips y navegación.
  - Inicialización de componentes y control de eventos.

### Flujo de Trabajo

1. **Inicio**: El usuario accede a la interfaz principal (`index.html`) y puede navegar entre chat, consulta web y gestor de archivos.
2. **Consulta por Chat**: El usuario envía preguntas, el sistema procesa el mensaje y responde usando IA contextual y/o búsqueda documental.
3. **Subida y Gestión de Archivos**: El usuario puede subir documentos, ver el listado, filtrar por tipo y descargar archivos.
4. **Acciones en el Chat**: El usuario puede copiar, escuchar, reintentar o compartir el historial del chat, con tooltips y feedback visual.

### Estructura de Carpetas

- `wwwroot/`
  - `index.html`, `buscar.html`, `buscarweb.html`, `upload.html`: Páginas principales.
  - `css/styles.css`: Estilos globales y personalizados.
  - `js/`: Scripts modulares por funcionalidad.
    - `chat.js`, `upload.js`, `document.js`, `download.js`, `utils.js`, `fileUI.js`, `nav.js`, `voice.js`
  - `lib/`, `img/`, `Uploads/`: Librerías, imágenes y archivos subidos.

### Dependencias y Requisitos

- **Frontend**: HTML5, CSS3, JavaScript ES6+, FontAwesome para iconos.
- **Backend**: API REST .NET (ver sección TextSimilarityApi).

---

## TextSimilarityApi

### Descripción General

TextSimilarityApi es el backend profesional para procesamiento de documentos, extracción de texto, generación de embeddings y búsqueda semántica. Está construido en .NET 9, con arquitectura modular y almacenamiento en SQLite, permitiendo consultas inteligentes sobre documentos subidos y videos procesados.

### Arquitectura y Componentes

- **API RESTful**: Controlador principal `EmbeddingController` expone endpoints para subida, búsqueda, descarga y listado de documentos.
- **Servicios**:
  - `EmbeddingService`: Obtiene embeddings y respuestas contextuales usando Azure OpenAI.
  - `TextExtractor`: Extrae texto de múltiples formatos (PDF, DOCX, XLSX, imágenes con OCR, videos).
  - `VideoProcessor`: Procesa videos, extrae audio y frames, realiza transcripción y OCR, genera embeddings segmentados.
  - `EmbeddingRepository`: Persiste embeddings y metadatos en SQLite.
  - `InMemoryDocumentStore`: Almacena documentos y embeddings en memoria por sesión, con TTL y limpieza automática.
  - `SimilarityCalculator`: Calcula similitud coseno entre vectores de embeddings.

### Principales Módulos

- **Controlador EmbeddingController**

  - `/upload`: Recibe archivos, extrae texto, genera embeddings y almacena resultados.
  - `/search`: Busca documentos relevantes por similitud semántica, usando embeddings y contexto de chat.
  - `/searchweb`: Realiza búsqueda web contextual, integrando historial y documentos de sesión.
  - `/download`: Permite descargar archivos originales o procesados.
  - `/documents`: Lista todos los documentos subidos y sus snippets.
  - `/documents/session`: Lista documentos de la sesión actual.

- **Servicios Clave**
  - `EmbeddingService`: Comunicación con Azure OpenAI para embeddings y respuestas contextuales.
  - `TextExtractor`: Soporta TXT, PDF, DOCX, XLSX, CSV, imágenes (OCR), videos (audio y frames).
  - `VideoProcessor`: Extrae audio con FFmpeg, transcribe con Vosk, realiza OCR en frames, genera embeddings segmentados.
  - `EmbeddingRepository`: Persistencia en SQLite, recuperación eficiente de documentos y embeddings.
  - `InMemoryDocumentStore`: Gestión temporal de documentos por sesión, evita duplicados y expira entradas antiguas.

### Flujo de Trabajo

1. **Subida de Documento**: El usuario envía un archivo vía `/upload`. El sistema extrae texto, genera embeddings y almacena resultados en SQLite y memoria.
2. **Búsqueda Semántica**: El usuario consulta vía `/search` o `/searchweb`. Se calcula la similitud coseno entre el embedding de la consulta y los documentos, retornando los más relevantes.
3. **Gestión de Documentos**: El usuario puede listar (`/documents`, `/documents/session`) y descargar (`/download`) archivos y snippets procesados.
4. **Procesamiento de Videos**: Los videos se segmentan, extraen audio y frames, se transcriben y procesan con OCR, generando embeddings por segmento.

### Endpoints Principales

| Endpoint                       | Método | Descripción                                                       |
| ------------------------------ | ------ | ----------------------------------------------------------------- |
| `/Embedding/upload`            | POST   | Sube archivo, extrae texto, genera embedding y almacena           |
| `/Embedding/search`            | POST   | Busca documentos por similitud semántica y contexto               |
| `/Embedding/searchweb`         | POST   | Búsqueda web contextual, integra historial y documentos de sesión |
| `/Embedding/download`          | GET    | Descarga archivo original o procesado                             |
| `/Embedding/documents`         | GET    | Lista todos los documentos y snippets                             |
| `/Embedding/documents/session` | GET    | Lista documentos de la sesión actual                              |

### Detalles Técnicos

- **Framework**: .NET 9, API REST con CORS habilitado para cualquier origen.
- **Persistencia**: SQLite (`embeddings.db`) para embeddings y metadatos.
- **Embeddings**: Azure OpenAI, configurable vía `appsettings.json`.
- **OCR**: Tesseract, con soporte para español e inglés (`tessdata/`).
- **Procesamiento de Video**: FFmpeg para extracción de audio/frames, Vosk para transcripción.
- **Soporte de Formatos**: TXT, PDF, DOCX, XLSX, CSV, imágenes (PNG, JPG, BMP, TIFF), videos (MP4, AVI, etc.).
- **Modularidad**: Servicios desacoplados, fácil extensión y mantenimiento.
- **Seguridad**: Validación de archivos, control de duplicados, TTL en memoria.

### Ejemplo de Configuración

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<tu-endpoint>.openai.azure.com/",
    "ApiKey": "<tu-api-key>",
    "Deployment": "<tu-deployment>",
    "ApiVersion": "2025-01-01-preview"
  },
  "Vosk": {
    "ModelPath": "tessdata/vosk-model-small-es-0.42"
  },
  "FFmpeg": {
    "Path": "C:/ffmpeg/bin/ffmpeg.exe"
  }
}
```

### Requisitos y Dependencias

- .NET 9
- SQLite
- Azure OpenAI
- Tesseract OCR (`tessdata/`)
- FFmpeg
- Vosk (modelos de voz)

### Extensibilidad y Buenas Prácticas

- Código desacoplado y orientado a servicios.
- Fácil integración de nuevos formatos y modelos.
- Documentación exhaustiva y comentarios en código.
- Ejemplos de uso y configuración incluidos.

---

### Endpoints y Comunicación

- `/api/chat/ask`: Consulta principal vía POST, recibe prompt y tipo.
- `/api/chat/askchar`: Consulta NLP básica vía POST.
- `/api/upload`: Subida de archivos (global o por sesión).
- `/api/documents`: Listado de documentos.
- `/api/download`: Descarga de archivos.

### Seguridad y Buenas Prácticas

- Escapado de HTML y atributos para prevenir XSS.
- Validación de extensiones y tipos de archivo.
- Modularidad y separación de responsabilidades en scripts.
- Tooltips y feedback visual para mejorar UX.

### Personalización y Extensibilidad

- Fácil integración de nuevos tipos de acción en el chat.
- Soporte para nuevos tipos de archivo y filtros.
- Estilos y componentes personalizables.

### Ejemplo de Uso

1. Sube un documento desde el gestor.
2. Realiza una consulta en el chat sobre el documento subido.
3. Descarga el documento o comparte el historial del chat.

---
