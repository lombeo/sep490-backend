using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sep490_Backend.DTO.Contract;
using System.Text.Json;

namespace Sep490_Backend.Infra.ModelBinders
{
    public class ContractDetailModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            // Lấy tên của model từ binding context
            var modelName = bindingContext.ModelName;

            // Lấy tất cả các giá trị có tên modelName từ form
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
            
            if (valueProviderResult == ValueProviderResult.None)
            {
                // Không có dữ liệu nào được tìm thấy, trả về thất bại
                bindingContext.Result = ModelBindingResult.Failed();
                return;
            }

            var values = valueProviderResult.Values;
            var result = new List<SaveContractDetailDTO>();

            // Xử lý từng giá trị và chuyển đổi thành đối tượng SaveContractDetailDTO
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        // Cố gắng parse JSON thành đối tượng SaveContractDetailDTO
                        var item = JsonSerializer.Deserialize<SaveContractDetailDTO>(value, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        // Thêm vào danh sách kết quả nếu hợp lệ
                        if (item != null && !string.IsNullOrEmpty(item.WorkName))
                        {
                            result.Add(item);
                        }
                    }
                    catch (JsonException ex)
                    {
                        // Log lỗi để debug
                        Console.WriteLine($"JSON Parse Error: {ex.Message}. Input: {value}");
                        continue;
                    }
                }
            }

            // Đặt kết quả vào binding context
            bindingContext.Result = ModelBindingResult.Success(result);
        }
    }

    public class ContractDetailModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Chỉ áp dụng cho danh sách SaveContractDetailDTO
            if (context.Metadata.ModelType == typeof(List<SaveContractDetailDTO>))
            {
                return new ContractDetailModelBinder();
            }

            return null;
        }
    }
} 