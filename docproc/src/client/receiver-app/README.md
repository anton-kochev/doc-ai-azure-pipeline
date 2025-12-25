# Receiver App

An Angular 20.3 web application for uploading documents to the document AI processing pipeline. Built with Angular Material and designed with a modern, responsive UI supporting both light and dark themes.

## Overview

The Receiver App is the frontend client for the document processing pipeline. It provides an intuitive drag-and-drop interface for users to upload documents that will be processed by the backend Azure Functions API and orchestration workers.

## Features

- **Drag-and-Drop Upload** - Intuitive file upload with drag-and-drop support
- **Click to Upload** - Traditional file picker for users who prefer clicking
- **Upload Progress** - Real-time progress tracking during file upload
- **Material Design** - Modern UI built with Angular Material components
- **Responsive Design** - Works seamlessly on desktop, tablet, and mobile devices
- **Theme Support** - Light and dark mode themes
- **Error Handling** - Clear error messages and validation feedback
- **File Preview** - Shows selected file information before upload
- **TypeScript 5.9** - Fully typed with strict mode enabled
- **Signals API** - Modern reactive state management using Angular signals

## Prerequisites

- [Node.js](https://nodejs.org/) (LTS version, 20.x or later)
- npm (comes with Node.js)

## Getting Started

### Installation

```bash
# Install dependencies
npm install
```

### Development Server

To start a local development server:

```bash
npm start
```

The application will be available at `http://localhost:4200/`. The app automatically reloads when you modify source files.

### Build

To build the project for production:

```bash
npm run build
```

The build artifacts will be stored in the `dist/` directory, optimized for performance and speed.

### Build with Watch Mode

For development with automatic rebuilds:

```bash
npm run watch
```

## Testing

### Unit Tests

Run unit tests with Karma test runner:

```bash
npm test
```

### Test Coverage

Run tests with code coverage report:

```bash
npm test -- --code-coverage
```

Coverage reports will be generated in the `coverage/` directory.

## Code Quality

### Linting

Check and fix code quality issues with ESLint:

```bash
npm run lint
```

### Formatting

Format code with Prettier:

```bash
# Check formatting
npm run format:check

# Fix formatting
npm run format
```

## Project Structure

```
src/
├── app/
│   ├── upload/                  # Upload component with drag-and-drop
│   ├── file-upload.service.ts   # Service for API communication
│   ├── app.component.ts         # Root component
│   └── app.config.ts            # Application configuration
├── assets/                      # Static assets
└── styles.scss                  # Global styles and Material theming
```

## Architecture

### Components

- **UploadComponent** - Main upload interface with drag-and-drop functionality
  - Uses Angular signals for reactive state management
  - OnPush change detection for optimal performance
  - Material Design components (buttons, progress bars)
  - BEM methodology for CSS organization

### Services

- **FileUploadService** - Handles HTTP communication with the backend API
  - Multipart/form-data upload to `/api/upload` endpoint
  - Progress tracking using HttpClient events
  - Error handling and retry logic

### Styling

- **SCSS** - Preprocessor for enhanced CSS capabilities
- **Material Design** - Comprehensive component library
- **BEM Methodology** - Block Element Modifier naming convention
- **Theme Support** - Light and dark color schemes
- **Responsive Grid** - Flexbox-based layout system

## Configuration

### API Endpoint

The API endpoint is configured in `src/environments/`:

- `environment.ts` - Development configuration
- `environment.prod.ts` - Production configuration

### TypeScript Configuration

The app uses strict TypeScript settings defined in `tsconfig.json`:

- Strict mode enabled
- Null checks enforced
- Explicit types required

## Integration with Backend

The app communicates with the document processing API:

- **Endpoint**: `POST /api/upload`
- **Content-Type**: `multipart/form-data`
- **Request**:
  - `file` - Document file (PDF, JPEG, PNG)
  - `extractionProfile` (optional) - Profile for document extraction
  - `tenantId` (optional) - Multi-tenant identifier
- **Response**: Job and document IDs for tracking processing status

## Deployment

The app is deployed to Azure Static Web Apps using GitHub Actions:

- **Workflow**: `.github/workflows/azure-static-web-apps-receiver.yml`
- **Trigger**: Push to `main` branch
- **Build**: `npm run build`
- **Output**: `dist/receiver-app/browser`

## Technology Stack

- **Angular** 20.3 - Framework
- **Angular Material** 20.2.7 - UI component library
- **RxJS** 7.8 - Reactive programming
- **TypeScript** 5.9 - Type-safe JavaScript
- **Karma** 6.4 - Test runner
- **Jasmine** 5.9 - Testing framework
- **ESLint** 9.35 - Code linting
- **Prettier** 3.6 - Code formatting

## Code Scaffolding

Generate new components, services, or other Angular artifacts:

```bash
# Generate a component
ng generate component component-name

# Generate a service
ng generate service service-name

# See all available schematics
ng generate --help
```

## Planned Features

The following features are planned for future releases:

### ManualReview Interface (Human-in-the-Loop)

A review UI for documents requiring manual intervention:

- **Review Dashboard** - List of jobs in ManualReview status
- **Document Viewer** - PDF preview with OCR results overlay
- **Extraction Editor** - Edit extracted fields with validation
- **Decision Actions**:
  - **Resume** - Continue processing from next stage
  - **Reject** - Mark document as failed with reason
- **Audit Trail** - View job history and previous review decisions

**Status**: Planned (Backend ManualReview workflow ✅ implemented, UI pending)

**API Integration**:

```typescript
// Get jobs in manual review
GET /api/jobs?status=ManualReview

// Get job details with extraction results
GET /api/jobs/{jobId}

// Send review decision
POST /api/jobs/{jobId}/review
{
  "decision": "RESUME" | "REJECT",
  "reason": "optional explanation"
}
```

This will integrate with the existing ManualReview state machine in the orchestrator, which currently supports external event handling via the Durable Functions HTTP API.

### Job Status Tracking

- **Job List** - View all submitted jobs with current status
- **Status Updates** - Real-time updates via SignalR or polling
- **Job Details** - View processing stage, timestamps, and errors
- **Retry Failed Jobs** - Resubmit jobs that failed processing

## Additional Resources

- [Angular Documentation](https://angular.dev)
- [Angular CLI Reference](https://angular.dev/tools/cli)
- [Angular Material Documentation](https://material.angular.io)
- [Project Root README](../../../../README.md) - Full project documentation
