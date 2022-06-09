using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using RabiRiichi.Server.Models;

namespace RabiRiichi.Server.Binders {
    public class RoomBinder : IModelBinder {
        private readonly RoomList roomList;

        public RoomBinder(RoomList roomList) {
            this.roomList = roomList;
        }

        public Task BindModelAsync(ModelBindingContext bindingContext) {
            if (bindingContext == null) {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var modelName = bindingContext.ModelName;

            // Try to fetch the value of the argument by name
            var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

            if (valueProviderResult == ValueProviderResult.None) {
                return Task.CompletedTask;
            }

            bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

            var value = valueProviderResult.FirstValue;

            // Check if the argument value is null or empty
            if (string.IsNullOrEmpty(value)) {
                return Task.CompletedTask;
            }

            if (!int.TryParse(value, out int id)) {
                // Non-integer arguments result in model state errors
                bindingContext.ModelState.TryAddModelError(
                    modelName, "Room number must be an integer.");
                return Task.CompletedTask;
            }

            // Model will be null if not found
            var model = roomList.Get(id);
            bindingContext.Result = ModelBindingResult.Success(model);
            return Task.CompletedTask;
        }
    }

    public class RoomBinderProvider : IModelBinderProvider {
        public IModelBinder GetBinder(ModelBinderProviderContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }
            if (context.Metadata.ModelType == typeof(Room)) {
                return new BinderTypeModelBinder(typeof(RoomBinder));
            }
            return null;
        }
    }
}