import { Injectable } from '@angular/core';
import { AppConfig } from './app-config';

const CONFIG_PATH = '/config.json';

@Injectable({
  providedIn: 'root',
})
export class ConfigService {
  #config: AppConfig | null = null;

  /**
   * Loads application configuration from config.json file.
   *
   * @returns Promise resolving to the loaded configuration
   * @throws {Error} If configuration fails to load or is invalid
   */
  public loadConfig(): Promise<void> {
    return fetch(CONFIG_PATH)
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Failed to load config: ${response.statusText}`);
        }
        return response.json();
      })
      .then((data: unknown) => {
        this.#config = this.#validateConfig(data);
      })
      .catch((error) => {
        console.error('Error loading configuration:', error);
        throw error;
      });
  }

  get apiUrl(): AppConfig['apiUrl'] {
    return this.#ensureLoaded().apiUrl;
  }

  get environment(): AppConfig['environment'] {
    return this.#ensureLoaded().environment;
  }

  get appName(): AppConfig['appName'] {
    return this.#ensureLoaded().appName;
  }

  get version(): AppConfig['version'] {
    return this.#ensureLoaded().version;
  }

  get features(): AppConfig['features'] {
    return this.#ensureLoaded().features;
  }

  get logging(): AppConfig['logging'] {
    return this.#ensureLoaded().logging;
  }

  #ensureLoaded(): AppConfig {
    if (!this.#config) {
      throw new Error('Configuration not loaded. Call loadConfig() first.');
    }
    return this.#config;
  }

  #isObject(value: unknown): value is Record<string, unknown> {
    return value !== null && typeof value === 'object';
  }

  #validateConfig(config: unknown): AppConfig {
    if (!this.#isObject(config)) {
      throw new Error('Invalid configuration: must be an object');
    }

    // Check and throw an error if config.json contains incorrect keys.
    // There is no type-safe between json and interface describes its structure,
    // so we have to check it manually for typos and mistakes
    const requiredFields: (keyof AppConfig)[] = [
      'apiUrl',
      'environment',
      'appName',
      'version',
      'features',
      'logging',
    ];

    for (const field of requiredFields) {
      if (!(field in config)) {
        throw new Error(`Missing required configuration field: ${field}`);
      }
    }

    return config as unknown as AppConfig;
  }
}
