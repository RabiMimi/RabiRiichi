using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using RabiRiichi.Server.Models;
using System.ComponentModel.DataAnnotations;

namespace RabiRiichi.Server.Binders {
    public class RequireAuthAttribute : RequiredAttribute {
        public RequireAuthAttribute() : base() {
            ErrorMessage = "Unauthenticated";
        }
    }
    public class AuthHeaderAttribute : FromHeaderAttribute {
        public AuthHeaderAttribute() : base() {
            Name = "Session-Code";
        }
    }

    public class AuthBinder : IModelBinder {
        private readonly UserList userList;

        public AuthBinder(UserList userList) {
            this.userList = userList;
        }

        public Task BindModelAsync(ModelBindingContext bindingContext) {
            if (bindingContext == null) {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var modelName = bindingContext.ModelName;

            // Try to fetch the value of the argument by name
            string value;

            if (bindingContext.BindingSource == BindingSource.Header) {
                value = bindingContext.HttpContext.Request.Headers[modelName];
            } else {
                var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

                if (valueProviderResult == ValueProviderResult.None) {
                    return Task.CompletedTask;
                }

                bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

                value = valueProviderResult.FirstValue;
            }

            // Check if the argument value is null or empty
            if (string.IsNullOrEmpty(value)) {
                return Task.CompletedTask;
            }

            long id;
            try {
                id = Convert.ToInt64(value, 16);
            } catch {
                // Non-integer arguments result in model state errors
                bindingContext.ModelState.TryAddModelError(
                    modelName, "Session code must be an integer.");
                return Task.CompletedTask;
            }

            // Model will be null if not found
            var model = userList.Get(id);
            bindingContext.Result = ModelBindingResult.Success(model);
            return Task.CompletedTask;
        }
    }

    public class AuthBinderProvider : IModelBinderProvider {
        public IModelBinder GetBinder(ModelBinderProviderContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }
            if (context.Metadata.ModelType == typeof(User)) {
                return new BinderTypeModelBinder(typeof(AuthBinder));
            }
            return null;
        }
    }
}