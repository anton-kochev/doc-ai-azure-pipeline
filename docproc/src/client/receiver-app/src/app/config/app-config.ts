export interface AppConfig {
  apiUrl: string;
  environment: string;
  appName: string;
  version: string;
  features: {
    fileUpload: boolean;
    darkMode: boolean;
  };
  logging: {
    level: string;
    enableConsole: boolean;
  };
}
