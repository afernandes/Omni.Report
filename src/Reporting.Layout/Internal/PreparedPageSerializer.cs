using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Reporting.Geometry;
using Reporting.Layout.Primitives;

namespace Reporting.Layout.Internal;

internal static class PreparedPageSerializer
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(LayoutPrimitive)) return;
            info.PolymorphismOptions = new();
            Type[] primitives = [typeof(DrawTextPrimitive), typeof(DrawLinePrimitive),
                typeof(DrawRectanglePrimitive), typeof(DrawEllipsePrimitive), typeof(DrawImagePrimitive),
                typeof(DrawPolygonPrimitive)];
            for (int i = 0; i < primitives.Length; i++)
                info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(primitives[i], i));
        });
        // Um campo float/double com NaN ou infinito pagina normalmente e vira SemanticValue; o JSON estrito
        // rejeitaria esse valor e abortaria o documento preparado inteiro. O formato é interno e temporário,
        // então aceitar os literais nomeados não cria problema de interoperabilidade.
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        options.Converters.Add(new PreparedArrayConverter<LayoutPrimitive>());
        options.Converters.Add(new PreparedArrayConverter<Point>());
        options.Converters.Add(new PreparedBytesConverter());
        options.Converters.Add(new PreparedScalarConverter());
        return options;
    }
}
