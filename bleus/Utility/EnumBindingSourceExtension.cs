using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace Utility
{
    internal class EnumBindingSourceExtension : MarkupExtension
    {
        private readonly Type _enumType;

        public EnumBindingSourceExtension(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException($"{enumType} is not enum.");
            }

            _enumType = enumType ?? throw new ArgumentNullException(nameof(enumType));
        }

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            Enum.GetValues(_enumType);
    }
}
