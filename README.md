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
- Visualización y descarga de documentos (PDF, TXT, DOCX, XLSX, CSV, imágenes, etc.).
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
