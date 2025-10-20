---
id: angular-core
title: Angular Core Style Guide
framework: angular
scope: general
version: 1.0.0
priority: must
appliesTo: ["**/*.ts", "**/*.html"]
---
### Angular General Principles

- Follow the [Angular Style Guide](https://angular.dev/style-guide)
- Use strict TypeScript mode with `strict: true`
- Enable `noImplicitAny`, `strictNullChecks`, and `strictPropertyInitialization`

### File Organization

- One component/service/directive per file
- Use index barrel exports sparingly (performance consideration)
- Group by feature, not by type

### Angular Naming Conventions

- **Components**: `feature-name.component.ts`
- **Services**: `feature-name.service.ts`
- **Modules**: `feature-name.module.ts`
- **Directives**: `feature-name.directive.ts`
- **Pipes**: `feature-name.pipe.ts`

### CSS/SCSS Styling

- Use BEM (Block Element Modifier) methodology for CSS class naming
- Block: represents a standalone component (e.g., `.user-card`)
- Element: part of a block, separated by `__` (e.g., `.user-card__header`)
- Modifier: variant or state, separated by `--` (e.g., `.user-card--featured`)
- Example: `.upload-form__submit-button--disabled`

### Angular Dependency Injection

- **MUST** use `inject()` function for dependency injection by default
- Constructor injection should only be used for backward compatibility with older Angular versions
- Services should be `providedIn: 'root'` unless scoped to a module
- Example:

  ```typescript
  export class MyComponent {
    private readonly myService = inject(MyService);
    private readonly http = inject(HttpClient);
  }
  ```

### RxJS

- Always unsubscribe from subscriptions (use `takeUntilDestroyed()` or async pipe)
- Prefer declarative patterns over imperative subscriptions
- Use `shareReplay()` for multicasted observables
