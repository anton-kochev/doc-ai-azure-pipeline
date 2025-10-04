---
id: typescript-core
title: TypeScript Core Style Guide
framework: typescript
scope: general
version: 1.0.0
priority: must
appliesTo: ["**/*.ts", "**/*.tsx"]
---

# TypeScript Core Style Guide

## General Principles

- Use strict TypeScript mode with `strict: true`
- Enable all recommended strict checks: `noImplicitAny`, `strictNullChecks`, `strictFunctionTypes`, `strictPropertyInitialization`
- Prefer explicit types over implicit inference for public APIs
- Use type inference for local variables where the type is obvious

## Type System

### Basic Types

- **Primitives**: Use lowercase `string`, `number`, `boolean`, never `String`, `Number`, `Boolean`
- **Arrays**: Prefer `Type[]` over `Array<Type>` for simple types
- **Objects**: Use interface for object shapes, type for unions/intersections
- **Unknown over Any**: Use `unknown` when type is uncertain, avoid `any`

```typescript
// Good
const name: string = "John";
const items: number[] = [1, 2, 3];
const value: unknown = getUserInput();

// Bad
const name: String = "John";
const items: Array<number> = [1, 2, 3];
const value: any = getUserInput();
```

### Interfaces vs Types

- **Interfaces**: For object shapes, especially when extending or implementing
- **Types**: For unions, intersections, mapped types, and utility types
- Interfaces can be augmented; types cannot

```typescript
// Interfaces for objects
interface User {
  id: string;
  name: string;
  email: string;
}

// Types for unions and complex types
type Status = 'pending' | 'active' | 'inactive';
type Result<T> = { success: true; data: T } | { success: false; error: string };
```

### Generics

- Use descriptive names for generic parameters when context is unclear
- Single letter (`T`, `K`, `V`) is acceptable for simple, obvious cases
- Constrain generics when possible with `extends`

```typescript
// Simple case
function identity<T>(value: T): T {
  return value;
}

// Descriptive names for clarity
function mapObject<TInput, TOutput>(
  obj: Record<string, TInput>,
  mapper: (value: TInput) => TOutput
): Record<string, TOutput> {
  // ...
}

// Constraints
function getProperty<T, K extends keyof T>(obj: T, key: K): T[K] {
  return obj[key];
}
```

## Naming Conventions

- **Variables/Functions**: camelCase (`userName`, `fetchData`)
- **Classes/Interfaces/Types**: PascalCase (`User`, `UserService`, `ResponseData`)
- **Enums**: PascalCase for enum name, UPPER_CASE for values
- **Constants**: UPPER_SNAKE_CASE for true constants (`MAX_RETRIES`, `API_BASE_URL`)
- **Private fields**: Prefix with `#` or `private` keyword (prefer `#`)
- **Boolean variables**: Use is/has/can prefix (`isLoading`, `hasAccess`, `canDelete`)

```typescript
// Constants
const MAX_RETRY_ATTEMPTS = 3;
const API_BASE_URL = 'https://api.example.com';

// Enums
enum UserRole {
  ADMIN = 'admin',
  USER = 'user',
  GUEST = 'guest'
}

// Classes with private fields
class UserManager {
  #users: User[] = [];

  public getUsers(): readonly User[] {
    return this.#users;
  }
}
```

## Functions

### Function Declarations

- Always specify return types for exported functions
- Use arrow functions for callbacks and short functions
- Use function declarations for named, reusable functions

```typescript
// Explicit return type
function calculateTotal(items: Item[]): number {
  return items.reduce((sum, item) => sum + item.price, 0);
}

// Arrow function for callbacks
const numbers = [1, 2, 3].map(n => n * 2);

// Async functions
async function fetchUser(id: string): Promise<User> {
  const response = await fetch(`/api/users/${id}`);
  return response.json();
}
```

### Function Overloads

- Use function overloads for different parameter combinations
- Place most specific overloads first
- Implementation signature should be compatible with all overloads

```typescript
function createDate(timestamp: number): Date;
function createDate(year: number, month: number, day: number): Date;
function createDate(yearOrTimestamp: number, month?: number, day?: number): Date {
  if (month !== undefined && day !== undefined) {
    return new Date(yearOrTimestamp, month, day);
  }
  return new Date(yearOrTimestamp);
}
```

## Classes

### Class Structure

- Order: static fields, instance fields, constructor, static methods, public instance methods, protected instance methods, private instance methods
- Always use `readonly` for immutable properties
- Prefer composition over inheritance
- Use access modifiers explicitly (`public`, `private`, `protected`)

```typescript
class UserService {
  // Static fields
  private static instance?: UserService;

  // Instance fields
  readonly #apiUrl: string;
  #cache: Map<string, User>;

  // Constructor
  constructor(apiUrl: string) {
    this.#apiUrl = apiUrl;
    this.#cache = new Map();
  }

  // Static methods
  static getInstance(apiUrl: string): UserService {
    if (!UserService.instance) {
      UserService.instance = new UserService(apiUrl);
    }
    return UserService.instance;
  }

  // Instance methods
  public async getUser(id: string): Promise<User> {
    const cached = this.#cache.get(id);
    if (cached) return cached;

    const user = await this.#fetchUser(id);
    this.#cache.set(id, user);
    return user;
  }
  
  #fetchUser(id: string): Promise<User> {
    // Private method implementation
  }
}
```

## Type Assertions and Guards

### Type Assertions

- Avoid type assertions (`as`) when possible
- Use type guards and narrowing instead
- Only use assertions when you have more information than TypeScript

```typescript
// Bad - unnecessary assertion
const value = getValue() as string;

// Good - type guard
function isString(value: unknown): value is string {
  return typeof value === 'string';
}

const value = getValue();
if (isString(value)) {
  console.log(value.toUpperCase());
}
```

### Type Guards

- Create custom type guards for complex types
- Use `is` predicate in return type

```typescript
interface Dog {
  bark(): void;
}

interface Cat {
  meow(): void;
}

function isDog(pet: Dog | Cat): pet is Dog {
  return 'bark' in pet;
}

function handlePet(pet: Dog | Cat): void {
  if (isDog(pet)) {
    pet.bark();
  } else {
    pet.meow();
  }
}
```

## Null and Undefined

- Prefer `undefined` over `null` for optional values
- Use optional chaining (`?.`) and nullish coalescing (`??`)
- Enable `strictNullChecks` to catch null/undefined errors

```typescript
// Optional properties with undefined
interface Config {
  timeout?: number; // undefined, not null
  retries?: number;
}

// Optional chaining
const port = config?.server?.port ?? 3000;

// Nullish coalescing (only null/undefined, not falsy values)
const timeout = config.timeout ?? 5000;
```

## Utility Types

Use TypeScript's built-in utility types:

```typescript
// Partial - make all properties optional
type PartialUser = Partial<User>;

// Required - make all properties required
type RequiredUser = Required<User>;

// Pick - select specific properties
type UserCredentials = Pick<User, 'email' | 'password'>;

// Omit - exclude specific properties
type UserWithoutPassword = Omit<User, 'password'>;

// Record - create object type with keys and values
type UserMap = Record<string, User>;

// ReadonlyArray - immutable array
type ImmutableUsers = ReadonlyArray<User>;
```

## Async/Await

- Prefer Promises over async/await for better control flow and composability
- Use `.then()` and `.catch()` for Promise chaining
- Return Promise types explicitly for async functions
- Use async/await sparingly, only when imperative control flow is necessary

```typescript
function fetchUserData(id: string): Promise<User> {
  return fetch(`/api/users/${id}`)
    .then(response => {
      if (!response.ok) {
        throw new Error(`Failed to fetch user: ${response.statusText}`);
      }
      return response.json();
    })
    .catch(error => {
      console.error('Error fetching user:', error);
      throw error;
    });
}

// Use async/await only when imperative control flow is needed
async function processMultipleUsers(ids: string[]): Promise<void> {
  for (const id of ids) {
    const user = await fetchUserData(id);
    await processUser(user);
  }
}
```

## Error Handling

- Create custom error classes for specific error types
- Use discriminated unions for result types
- **Never** use empty catch blocks

```typescript
// Custom error classes
class ValidationError extends Error {
  constructor(
    message: string,
    public readonly field: string
  ) {
    super(message);
    this.name = 'ValidationError';
  }
}

// Result type pattern
type Result<T, E = Error> =
  | { success: true; data: T }
  | { success: false; error: E };

async function saveUser(user: User): Promise<Result<User>> {
  try {
    const saved = await db.save(user);
    return { success: true, data: saved };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error : new Error('Unknown error')
    };
  }
}
```

## Imports and Exports

- Use named exports over default exports
- Group imports: external libraries, internal modules, types
- Use path aliases for cleaner imports

```typescript
// Imports grouped by type
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { UserService } from './user.service';
import { AuthGuard } from '@/guards/auth.guard';

import type { User, Role } from '@/types';

// Named exports
export class UserManager { }
export interface UserConfig { }
export type UserRole = 'admin' | 'user';
```

## Best Practices

### Immutability

- Prefer `const` over `let`, never use `var`
- Use `readonly` for properties that shouldn't change
- Use `ReadonlyArray<T>` or `readonly T[]` for immutable arrays

```typescript
// Readonly properties
interface Config {
  readonly apiUrl: string;
  readonly timeout: number;
}

// Readonly arrays
function processItems(items: readonly Item[]): void {
  // items.push() // Error: Property 'push' does not exist
  const doubled = items.map(item => item.value * 2);
}
```

### Avoid Magic Numbers

- Extract constants with meaningful names
- Use enums for related constants

```typescript
// Bad
if (user.age >= 18 && user.status === 1) { }

// Good
const MINIMUM_AGE = 18;
const ACTIVE_STATUS = 1;

if (user.age >= MINIMUM_AGE && user.status === ACTIVE_STATUS) { }

// Better with enum
enum UserStatus {
  INACTIVE = 0,
  ACTIVE = 1,
  SUSPENDED = 2
}

if (user.age >= MINIMUM_AGE && user.status === UserStatus.ACTIVE) { }
```

### Documentation

- Use JSDoc comments for public APIs
- Document complex logic and business rules
- Include `@param`, `@returns`, `@throws` tags

```typescript
/**
 * Calculates the total price including tax and discounts.
 *
 * @param items - Array of items to calculate total for
 * @param taxRate - Tax rate as decimal (e.g., 0.08 for 8%)
 * @param discountCode - Optional discount code to apply
 * @returns The total price including tax and discounts
 * @throws {ValidationError} If items array is empty
 */
function calculateTotal(
  items: Item[],
  taxRate: number,
  discountCode?: string
): number {
  if (items.length === 0) {
    throw new ValidationError('Items array cannot be empty', 'items');
  }
  // Implementation...
}
```

## Performance Considerations

- Use `const enum` for compile-time constants (zero runtime cost)
- Avoid expensive computations in type guards
- Use lazy initialization for heavy objects

```typescript
// const enum for zero runtime cost
const enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3
}

// Lazy initialization
class ExpensiveService {
  #instance?: HeavyObject;

  getInstance(): HeavyObject {
    if (!this.#instance) {
      this.#instance = new HeavyObject();
    }
    return this.#instance;
  }
}
```
