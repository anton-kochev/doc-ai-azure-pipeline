---
id: angular-components
title: Angular Component Style Rules
framework: angular
scope: structure
version: 1.0.0
priority: must
appliesTo: ["**/*.component.ts"]
---

# Angular Component Style Rules

## Component Structure

Components should follow this order:
1. Decorators (`@Component`)
2. Public properties (inputs, outputs)
3. Private properties
4. Constructor or `inject()` calls
5. Lifecycle hooks (in Angular lifecycle order)
6. Public methods
7. Private methods

## Inputs and Outputs

- Use `input()` and `output()` signals (Angular 17.1+)
- For legacy code: use `@Input()` and `@Output()` decorators
- Prefix output event names with verb (e.g., `userSelected`, `dataLoaded`)

## Template Guidelines

- Keep templates simple; move complex logic to component class
- Use `@if`, `@for`, `@switch` control flow (Angular 17+)
- For legacy: use `*ngIf`, `*ngFor`, `*ngSwitch`
- Avoid inline function calls in templates
- Use `trackBy` for `@for` / `*ngFor` when rendering lists

## Change Detection

- Prefer `OnPush` change detection strategy
- Use signals for reactive state management
- Avoid mutating objects; prefer immutable updates

## Styling

- Use SCSS for component styles
- Follow BEM (Block Element Modifier) naming convention:
  - Block: component name (e.g., `.user-card`)
  - Element: use `__` separator (e.g., `.user-card__title`)
  - Modifier: use `--` separator (e.g., `.user-card--highlighted`)
- Scope styles to component using `:host` selector when needed
- Avoid deep selectors (`::ng-deep`) unless absolutely necessary

## Example

```typescript
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-user-card',
  styleUrl: './user-card.component.scss',
  templateUrl: './user-card.component.html',
})
export class UserCardComponent {
  // Inputs
  user = input.required<User>();

  // Outputs
  userSelected = output<User>();

  // Private services
  private readonly logger = inject(LoggerService);

  // Lifecycle
  ngOnInit(): void {
    this.logger.log('UserCard initialized');
  }

  // Public methods
  selectUser(): void {
    this.userSelected.emit(this.user());
  }
}
```

### Template Example (user-card.component.html)

```html
<div class="user-card user-card--highlighted">
  <div class="user-card__header">
    <h3 class="user-card__title">{{ user().name }}</h3>
  </div>
  <div class="user-card__body">
    <p class="user-card__description">{{ user().bio }}</p>
  </div>
  <button class="user-card__action-button" (click)="selectUser()">
    Select
  </button>
</div>
```

### Style Example (user-card.component.scss)

```scss
.user-card {
  border: 1px solid #ddd;
  padding: 16px;
  border-radius: 4px;

  &--highlighted {
    border-color: #007bff;
    background-color: #f0f8ff;
  }

  &__header {
    margin-bottom: 12px;
  }

  &__title {
    font-size: 18px;
    font-weight: 600;
    margin: 0;
  }

  &__body {
    margin-bottom: 16px;
  }

  &__description {
    color: #666;
    margin: 0;
  }

  &__action-button {
    padding: 8px 16px;
    background-color: #007bff;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;

    &:hover {
      background-color: #0056b3;
    }

    &:disabled {
      background-color: #ccc;
      cursor: not-allowed;
    }
  }
}
```
