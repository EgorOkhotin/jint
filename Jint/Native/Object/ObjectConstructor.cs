﻿using System.Collections.Generic;
using System.Linq;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : FunctionInstance, IConstructor
    {
        private ObjectConstructor(Engine engine) : base(engine, null, null, false)
        {
        }

        public static ObjectConstructor CreateObjectConstructor(Engine engine)
        {
            var obj = new ObjectConstructor(engine);
            obj.Extensible = true;

            obj.PrototypeObject = ObjectPrototype.CreatePrototypeObject(engine, obj);

            obj.SetOwnProperty("length", new PropertyDescriptor(1, PropertyFlag.AllForbidden));
            obj.SetOwnProperty("prototype", new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden));

            return obj;
        }

        public void Configure()
        {
            Prototype = Engine.Function.PrototypeObject;

            FastAddProperty("getPrototypeOf", new ClrFunctionInstance(Engine, GetPrototypeOf, 1), true, false, true);
            FastAddProperty("getOwnPropertyDescriptor", new ClrFunctionInstance(Engine, GetOwnPropertyDescriptor, 2), true, false, true);
            FastAddProperty("getOwnPropertyNames", new ClrFunctionInstance(Engine, GetOwnPropertyNames, 1), true, false, true);
            FastAddProperty("create", new ClrFunctionInstance(Engine, Create, 2), true, false, true);
            FastAddProperty("defineProperty", new ClrFunctionInstance(Engine, DefineProperty, 3), true, false, true);
            FastAddProperty("defineProperties", new ClrFunctionInstance(Engine, DefineProperties, 2), true, false, true);
            FastAddProperty("seal", new ClrFunctionInstance(Engine, Seal, 1), true, false, true);
            FastAddProperty("freeze", new ClrFunctionInstance(Engine, Freeze, 1), true, false, true);
            FastAddProperty("preventExtensions", new ClrFunctionInstance(Engine, PreventExtensions, 1), true, false, true);
            FastAddProperty("isSealed", new ClrFunctionInstance(Engine, IsSealed, 1), true, false, true);
            FastAddProperty("isFrozen", new ClrFunctionInstance(Engine, IsFrozen, 1), true, false, true);
            FastAddProperty("isExtensible", new ClrFunctionInstance(Engine, IsExtensible, 1), true, false, true);
            FastAddProperty("keys", new ClrFunctionInstance(Engine, Keys, 1), true, false, true);
        }

        public ObjectPrototype PrototypeObject { get; private set; }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.1.1
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Construct(arguments);
            }

            if(arguments[0].IsNull() || arguments[0].IsUndefined())
            {
                return Construct(arguments);
            }

            return TypeConverter.ToObject(_engine, arguments[0]);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            if (arguments.Length > 0)
            {
                var value = arguments[0];
                var valueObj = value.TryCast<ObjectInstance>();
                if (!ReferenceEquals(valueObj, null))
                {
                    return valueObj;
                }
                var type = value.Type;
                if (type == Types.String || type == Types.Number || type == Types.Boolean)
                {
                    return TypeConverter.ToObject(_engine, value);
                }
            }

            var obj = new ObjectInstance(_engine)
                {
                    Extensible = true,
                    Prototype = Engine.Object.PrototypeObject
                };

            return obj;
        }
        
        internal ObjectInstance Construct(int propertyCount)
        {
            var obj = new ObjectInstance(_engine)
            {
                Extensible = true,
                Prototype = Engine.Object.PrototypeObject,
                _properties =  propertyCount > 0 ? new Dictionary<string, PropertyDescriptor>(propertyCount) : null
            };

            return obj;
        }

        public JsValue GetPrototypeOf(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return o.Prototype ?? Null;
        }

        public JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToString(p);

            var desc = o.GetOwnProperty(name);
            return PropertyDescriptor.FromPropertyDescriptor(Engine, desc);
        }

        public JsValue GetOwnPropertyNames(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            uint n = 0;

            ArrayInstance array = null;
            var ownProperties = o.GetOwnProperties().ToList();
            if (o is StringInstance s)
            {
                var length = s.PrimitiveValue.Length;
                array = Engine.Array.ConstructFast((uint) (ownProperties.Count + length));
                for (var i = 0; i < length; i++)
                {
                    array.SetIndexValue(n, TypeConverter.ToString(i), updateLength: false);
                    n++;
                }
            }

            array = array ?? Engine.Array.ConstructFast((uint) ownProperties.Count);
            for (var i = 0; i < ownProperties.Count; i++)
            {
                var p = ownProperties[i];
                array.SetIndexValue(n, p.Key, false);
                n++;
            }

            array.SetLength(n);
            return array;
        }

        public JsValue Create(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);

            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null) && !oArg.IsNull())
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var obj = Engine.Object.Construct(Arguments.Empty);
            obj.Prototype = o;

            var properties = arguments.At(1);
            if (!properties.IsUndefined())
            {
                var jsValues = _engine._jsValueArrayPool.RentArray(2);
                jsValues[0] = obj;
                jsValues[1] = properties;
                DefineProperties(thisObject, jsValues);
                _engine._jsValueArrayPool.ReturnArray(jsValues);
            }

            return obj;
        }

        public JsValue DefineProperty(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var p = arguments.At(1);
            var name = TypeConverter.ToString(p);

            var attributes = arguments.At(2);
            var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, attributes);

            o.DefineOwnProperty(name, desc, true);
            return o;
        }

        public JsValue DefineProperties(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var properties = arguments.At(1);
            var props = TypeConverter.ToObject(Engine, properties);
            var descriptors = new List<KeyValuePair<string, PropertyDescriptor>>();
            foreach (var p in props.GetOwnProperties())
            {
                if (!p.Value.Enumerable)
                {
                    continue;
                }

                var descObj = props.Get(p.Key);
                var desc = PropertyDescriptor.ToPropertyDescriptor(Engine, descObj);
                descriptors.Add(new KeyValuePair<string, PropertyDescriptor>(p.Key, desc));
            }
            foreach (var pair in descriptors)
            {
                o.DefineOwnProperty(pair.Key, pair.Value, true);
            }

            return o;
        }

        public JsValue Seal(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var properties = new List<KeyValuePair<string, PropertyDescriptor>>(o.GetOwnProperties());
            foreach (var prop in properties)
            {
                var propertyDescriptor = prop.Value;
                if (propertyDescriptor.Configurable)
                {
                    propertyDescriptor.Configurable = false;
                    FastSetProperty(prop.Key, propertyDescriptor);
                }

                o.DefineOwnProperty(prop.Key, propertyDescriptor, true);
            }

            o.Extensible = false;

            return o;
        }

        public JsValue Freeze(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var properties = new List<KeyValuePair<string, PropertyDescriptor>>(o.GetOwnProperties());
            foreach (var p in properties)
            {
                var desc = o.GetOwnProperty(p.Key);
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable)
                    {
                        var mutable = desc as PropertyDescriptor ?? new PropertyDescriptor(desc);
                        mutable.Writable = false;
                        desc = mutable;
                    }
                }
                if (desc.Configurable)
                {
                    var mutable = desc as PropertyDescriptor ?? new PropertyDescriptor(desc);
                    mutable.Configurable = false;
                    desc = mutable;
                }
                o.DefineOwnProperty(p.Key, desc, true);
            }

            o.Extensible = false;

            return o;
        }

        public JsValue PreventExtensions(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            o.Extensible = false;

            return o;
        }

        public JsValue IsSealed(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            foreach (var prop in o.GetOwnProperties())
            {
                if (prop.Value.Configurable)
                {
                    return false;
                }
            }

            if (o.Extensible == false)
            {
                return true;
            }

            return false;
        }

        public JsValue IsFrozen(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            foreach (var pair in o.GetOwnProperties())
            {
                var desc = pair.Value;
                if (desc.IsDataDescriptor())
                {
                    if (desc.Writable)
                    {
                        return false;
                    }
                }
                if (desc.Configurable)
                {
                    return false;
                }
            }

            if (o.Extensible == false)
            {
                return true;
            }

            return false;
        }

        public JsValue IsExtensible(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return o.Extensible;
        }

        public JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            var oArg = arguments.At(0);
            var o = oArg.TryCast<ObjectInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var enumerableProperties = o.GetOwnProperties()
                .Where(x => x.Value.Enumerable)
                .ToArray();
            var n = enumerableProperties.Length;

            var array = Engine.Array.ConstructFast((uint) n);
            uint index = 0;
            foreach (var prop in enumerableProperties)
            {
                var p = prop.Key;
                array.SetIndexValue(index, p, updateLength: false);
                index++;
            }
            return array;
        }
    }
}
