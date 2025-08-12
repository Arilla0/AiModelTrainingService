# AI Model Training Service

Сервис для обучения AI моделей, построенный на .NET 8.0 с использованием Clean Architecture.

## Архитектура проекта

Проект организован в виде многослойной архитектуры с четырьмя основными проектами:

### 🏗️ Структура проектов

- **AiModelTrainingService.Core** - Доменные модели, интерфейсы и перечисления
- **AiModelTrainingService.Infrastructure** - Реализация репозиториев, Entity Framework контекст
- **AiModelTrainingService.Services** - Бизнес-логика и сервисы приложения
- **AiModelTrainingService.Api** - Web API контроллеры и конфигурация

### 🎯 Используемые паттерны

- **Repository Pattern** - Абстракция доступа к данным
- **Unit of Work Pattern** - Управление транзакциями
- **Dependency Injection** - Инверсия зависимостей
- **Clean Architecture** - Разделение ответственности по слоям

## 🚀 Технологии

- **.NET 8.0** - Основная платформа
- **Entity Framework Core** - ORM для работы с базой данных
- **Entity Framework InMemory** - In-memory база данных для разработки
- **ASP.NET Core Web API** - REST API
- **Swagger/OpenAPI** - Документация API

## 📊 Доменные модели

### ModelDefinition
Определение AI модели с конфигурацией и метаданными.

### TrainingJob
Задача обучения модели с параметрами и метриками.

### Dataset
Набор данных для обучения моделей.

### TrainingMetric
Метрики процесса обучения (точность, потери и т.д.).

## 🔧 Запуск проекта

### Предварительные требования
- .NET 8.0 SDK

### Команды для запуска

```bash
# Клонирование и переход в директорию
cd AiModelTrainingService

# Восстановление пакетов
dotnet restore

# Сборка проекта
dotnet build

# Запуск API
cd AiModelTrainingService.Api
dotnet run
```

API будет доступен по адресу: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

## 📚 API Endpoints

### Models (Модели)
- `GET /api/models` - Получить все модели
- `GET /api/models/{id}` - Получить модель по ID
- `POST /api/models` - Создать новую модель
- `PUT /api/models/{id}` - Обновить модель
- `DELETE /api/models/{id}` - Удалить модель
- `POST /api/models/{id}/training` - Запустить обучение модели
- `GET /api/models/{id}/training` - Получить задачи обучения модели

### Training Jobs (Задачи обучения)
- `GET /api/trainingjobs/{id}` - Получить задачу обучения
- `POST /api/trainingjobs/{id}/cancel` - Отменить обучение
- `GET /api/trainingjobs/{id}/metrics` - Получить метрики обучения

### Datasets (Наборы данных)
- `GET /api/datasets` - Получить все наборы данных
- `GET /api/datasets/{id}` - Получить набор данных по ID
- `POST /api/datasets` - Создать новый набор данных
- `PUT /api/datasets/{id}` - Обновить набор данных
- `DELETE /api/datasets/{id}` - Удалить набор данных
- `POST /api/datasets/{datasetId}/models/{modelId}` - Привязать набор данных к модели
- `DELETE /api/datasets/{datasetId}/models/{modelId}` - Отвязать набор данных от модели

## 🧪 Примеры использования

### Создание модели
```bash
curl -X POST http://localhost:5000/api/models \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Image Classification Model",
    "description": "Модель для классификации изображений",
    "type": 0,
    "configuration": "{\"algorithm\": \"CNN\", \"layers\": 5}",
    "createdBy": "developer"
  }'
```

### Запуск обучения
```bash
curl -X POST http://localhost:5000/api/models/{modelId}/training \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "Training Job 1",
    "parameters": "{\"learning_rate\": 0.001, \"batch_size\": 32}",
    "epochs": 10
  }'
```

### Создание набора данных
```bash
curl -X POST http://localhost:5000/api/datasets \
  -H "Content-Type: application/json" \
  -d '{
    "name": "CIFAR-10 Dataset",
    "description": "Набор данных для классификации изображений",
    "filePath": "/data/cifar10.csv",
    "format": "CSV",
    "fileSize": 1048576,
    "recordCount": 50000,
    "createdBy": "data-engineer"
  }'
```

## 🏛️ Архитектурные особенности

### Dependency Injection
Все зависимости регистрируются в `ServiceCollectionExtensions`:
- `AddInfrastructure()` - регистрация репозиториев и EF контекста
- `AddServices()` - регистрация бизнес-сервисов

### Entity Framework Configuration
- Использование Fluent API для конфигурации моделей
- In-Memory база данных для разработки и тестирования
- Автоматическая инициализация базы данных при запуске

### Обработка ошибок
- Стандартная обработка HTTP статусов (404, 400, etc.)
- Валидация входных данных через модели запросов

## 🔮 Возможности расширения

1. **Добавление реальной базы данных** (SQL Server, PostgreSQL)
2. **Интеграция с ML.NET** для реального обучения моделей
3. **Добавление аутентификации и авторизации**
4. **Реализация фоновых задач** для длительного обучения
5. **Добавление логирования** (Serilog, NLog)
6. **Метрики и мониторинг** (Prometheus, Application Insights)
7. **Контейнеризация** (Docker)
8. **Тестирование** (Unit tests, Integration tests)

## 📝 Статусы и перечисления

### ModelStatus
- `Created` - Модель создана
- `Training` - Модель обучается
- `Trained` - Модель обучена
- `Failed` - Ошибка обучения
- `Deployed` - Модель развернута
- `Archived` - Модель архивирована

### ModelType
- `Classification` - Классификация
- `Regression` - Регрессия
- `NeuralNetwork` - Нейронная сеть
- `DeepLearning` - Глубокое обучение
- `NaturalLanguageProcessing` - Обработка естественного языка
- `ComputerVision` - Компьютерное зрение

### TrainingStatus
- `Pending` - Ожидает запуска
- `InProgress` - Выполняется
- `Completed` - Завершено
- `Failed` - Ошибка
- `Cancelled` - Отменено

---

**Разработано с использованием .NET 8.0 и Clean Architecture принципов**
