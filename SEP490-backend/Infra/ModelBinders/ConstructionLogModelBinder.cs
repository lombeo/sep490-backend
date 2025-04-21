using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sep490_Backend.DTO.ConstructionLog;
using System.Text.Json;

namespace Sep490_Backend.Infra.ModelBinders
{
    /// <summary>
    /// Custom model binder for handling construction log arrays that come as JSON strings in form data
    /// </summary>
    public class ConstructionLogArrayModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            // Get the model name from binding context
            var modelName = bindingContext.ModelName;

            // Get all values with modelName from form
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
            
            if (valueProviderResult == ValueProviderResult.None)
            {
                // No data found, return failure
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }

            var values = valueProviderResult.Values;
            
            // Determine the type of list based on the model type
            Type itemType = null;
            
            if (bindingContext.ModelType == typeof(List<ConstructionLogResourceDTO>))
            {
                itemType = typeof(ConstructionLogResourceDTO);
            }
            else if (bindingContext.ModelType == typeof(List<WorkAmountDTO>))
            {
                itemType = typeof(WorkAmountDTO);
            }
            else if (bindingContext.ModelType == typeof(List<WeatherDTO>))
            {
                itemType = typeof(WeatherDTO);
            }
            else
            {
                // Not a supported list type
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }
            
            // Create a generic list of the appropriate type
            var listType = typeof(List<>).MakeGenericType(itemType);
            var result = Activator.CreateInstance(listType) as System.Collections.IList;
            
            // Special case for array values
            if (values.Count == 1)
            {
                var value = values[0];
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        // Try to parse as an array first
                        if (value.StartsWith("[") && value.EndsWith("]"))
                        {
                            // Parse JSON array
                            var items = JsonSerializer.Deserialize(value, listType, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                
                            if (items != null)
                            {
                                bindingContext.Result = ModelBindingResult.Success(items);
                                return Task.CompletedTask;
                            }
                        }
                        else
                        {
                            // Try to parse as a single item
                            var item = JsonSerializer.Deserialize(value, itemType, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                
                            if (item != null)
                            {
                                // Add to list and return
                                result.Add(item);
                                bindingContext.Result = ModelBindingResult.Success(result);
                                return Task.CompletedTask;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        // Log error for debugging
                        Console.WriteLine($"JSON Parse Error: {ex.Message}. Input: {value}");
                    }
                }
            }
            else
            {
                // Process each value and convert to the appropriate object
                foreach (var value in values)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            // Try to parse JSON to object
                            var item = JsonSerializer.Deserialize(value, itemType, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                
                            if (item != null)
                            {
                                result.Add(item);
                            }
                        }
                        catch (JsonException ex)
                        {
                            // Log error for debugging
                            Console.WriteLine($"JSON Parse Error: {ex.Message}. Input: {value}");
                            continue;
                        }
                    }
                }
            }
            
            // Set result in binding context
            if (result.Count > 0)
            {
                bindingContext.Result = ModelBindingResult.Success(result);
            }
            else
            {
                bindingContext.Result = ModelBindingResult.Failed();
            }
            
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Model binder provider for construction log-related array types
    /// </summary>
    public class ConstructionLogArrayModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Apply to known array types used in SaveConstructionLogDTO
            if (context.Metadata.ModelType == typeof(List<ConstructionLogResourceDTO>) ||
                context.Metadata.ModelType == typeof(List<WorkAmountDTO>) ||
                context.Metadata.ModelType == typeof(List<WeatherDTO>))
            {
                return new ConstructionLogArrayModelBinder();
            }

            return null;
        }
    }
} 