/*
 * This file is part of LuaInterface.
 * 
 * Copyright (C) 2003-2005 Fabio Mascarenhas de Queiroz.
 * Copyright (C) 2012 Megax <http://megax.yeahunter.hu/>
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System;
using System.Reflection;
using System.Collections.Generic;
using LuaInterface.Extensions;

namespace LuaInterface
{
	/*
	 * Type checking and conversion functions.
	 * 
	 * Author: Fabio Mascarenhas
	 * Version: 1.0
	 */
	class CheckType
	{
		private Dictionary<long, ExtractValue> extractValues = new Dictionary<long, ExtractValue>();
		private ExtractValue extractNetObject;
		private ObjectTranslator translator;

		public CheckType(ObjectTranslator translator) 
		{
			this.translator = translator;
			extractValues.Add(typeof(object).TypeHandle.Value.ToInt64(), new ExtractValue(getAsObject));
			extractValues.Add(typeof(sbyte).TypeHandle.Value.ToInt64(), new ExtractValue(getAsSbyte));
			extractValues.Add(typeof(byte).TypeHandle.Value.ToInt64(), new ExtractValue(getAsByte));
			extractValues.Add(typeof(short).TypeHandle.Value.ToInt64(), new ExtractValue(getAsShort));
			extractValues.Add(typeof(ushort).TypeHandle.Value.ToInt64(), new ExtractValue(getAsUshort));
			extractValues.Add(typeof(int).TypeHandle.Value.ToInt64(), new ExtractValue(getAsInt));
			extractValues.Add(typeof(uint).TypeHandle.Value.ToInt64(), new ExtractValue(getAsUint));
			extractValues.Add(typeof(long).TypeHandle.Value.ToInt64(), new ExtractValue(getAsLong));
			extractValues.Add(typeof(ulong).TypeHandle.Value.ToInt64(), new ExtractValue(getAsUlong));
			extractValues.Add(typeof(double).TypeHandle.Value.ToInt64(), new ExtractValue(getAsDouble));
			extractValues.Add(typeof(char).TypeHandle.Value.ToInt64(), new ExtractValue(getAsChar));
			extractValues.Add(typeof(float).TypeHandle.Value.ToInt64(), new ExtractValue(getAsFloat));
			extractValues.Add(typeof(decimal).TypeHandle.Value.ToInt64(), new ExtractValue(getAsDecimal));
			extractValues.Add(typeof(bool).TypeHandle.Value.ToInt64(), new ExtractValue(getAsBoolean));
			extractValues.Add(typeof(string).TypeHandle.Value.ToInt64(), new ExtractValue(getAsString));
			extractValues.Add(typeof(LuaFunction).TypeHandle.Value.ToInt64(), new ExtractValue(getAsFunction));
			extractValues.Add(typeof(LuaTable).TypeHandle.Value.ToInt64(), new ExtractValue(getAsTable));
			extractValues.Add(typeof(LuaUserData).TypeHandle.Value.ToInt64(), new ExtractValue(getAsUserdata));
			extractNetObject = new ExtractValue(getAsNetObject);		
		}

		/*
		 * Checks if the value at Lua stack index stackPos matches paramType, 
		 * returning a conversion function if it does and null otherwise.
		 */
		internal ExtractValue getExtractor(IReflect paramType)
		{
			return getExtractor(paramType.UnderlyingSystemType);
		}

		internal ExtractValue getExtractor(Type paramType) 
		{
			if(paramType.IsByRef)
				paramType = paramType.GetElementType();

			long runtimeHandleValue = paramType.TypeHandle.Value.ToInt64();
			return extractValues.ContainsKey(runtimeHandleValue) ? extractValues[runtimeHandleValue] : extractNetObject;
		}

		internal ExtractValue checkType(KopiLua.Lua.lua_State luaState, int stackPos, Type paramType) 
		{
			var luatype = KopiLua.Lua.lua_type(luaState, stackPos).ToLuaTypes();

			if(paramType.IsByRef)
				paramType = paramType.GetElementType();

			var underlyingType = Nullable.GetUnderlyingType(paramType);

			if(!underlyingType.IsNull())
				paramType = underlyingType;	 // Silently convert nullable types to their non null requics

			long runtimeHandleValue = paramType.TypeHandle.Value.ToInt64();

			if(paramType.Equals(typeof(object)))
				return extractValues[runtimeHandleValue];

			//CP: Added support for generic parameters
			if(paramType.IsGenericParameter)
			{
				if(luatype == LuaTypes.Boolean)
					return extractValues[typeof(bool).TypeHandle.Value.ToInt64()];
				else if(luatype == LuaTypes.String)
					return extractValues[typeof(string).TypeHandle.Value.ToInt64()];
				else if(luatype == LuaTypes.Table)
					return extractValues[typeof(LuaTable).TypeHandle.Value.ToInt64()];
				else if(luatype == LuaTypes.UserData)
					return extractValues[typeof(object).TypeHandle.Value.ToInt64()];
				else if(luatype == LuaTypes.Function)
					return extractValues[typeof(LuaFunction).TypeHandle.Value.ToInt64()];
				else if(luatype == LuaTypes.Number)
					return extractValues[typeof(double).TypeHandle.Value.ToInt64()];
				//else
					//;//an unsupported type was encountered
			}

			if(KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return extractValues[runtimeHandleValue];

			if(paramType == typeof(bool))
			{
				if(KopiLua.Lua.lua_isboolean(luaState, stackPos))
					return extractValues[runtimeHandleValue];
			}
			else if(paramType == typeof(string))
			{
				if(KopiLua.Lua.lua_isstring(luaState, stackPos).ToBoolean())
					return extractValues[runtimeHandleValue];
				else if(luatype == LuaTypes.Nil)
					return extractNetObject; // kevinh - silently convert nil to a null string pointer
			}
			else if(paramType == typeof(LuaTable))
			{
				if(luatype == LuaTypes.Table)
					return extractValues[runtimeHandleValue];
			}
			else if(paramType == typeof(LuaUserData))
			{
				if(luatype == LuaTypes.UserData)
					return extractValues[runtimeHandleValue];
			}
			else if(paramType == typeof(LuaFunction))
			{
				if(luatype == LuaTypes.Function)
					return extractValues[runtimeHandleValue];
			}
			else if(typeof(Delegate).IsAssignableFrom(paramType) && luatype == LuaTypes.Function)
				return new ExtractValue(new DelegateGenerator(translator, paramType).extractGenerated);
			else if(paramType.IsInterface && luatype == LuaTypes.Table)
				return new ExtractValue(new ClassGenerator(translator, paramType).extractGenerated);
			else if((paramType.IsInterface || paramType.IsClass) && luatype == LuaTypes.Nil)
			{
				// kevinh - allow nil to be silently converted to null - extractNetObject will return null when the item ain't found
				return extractNetObject;
			}
			else if(KopiLua.Lua.lua_type(luaState, stackPos).ToLuaTypes() == LuaTypes.Table)
			{
				if(KopiLua.Lua.luaL_getmetafield(luaState, stackPos, "__index").ToBoolean())
				{
					object obj = translator.getNetObject(luaState, -1);
					KopiLua.Lua.lua_settop(luaState, -2);
					if(!obj.IsNull() && paramType.IsAssignableFrom(obj.GetType()))
						return extractNetObject;
				}
				else
					return null;
			}
			else
			{
				object obj = translator.getNetObject(luaState, stackPos);
				if(!obj.IsNull() && paramType.IsAssignableFrom(obj.GetType()))
					return extractNetObject;
			}

			return null;
		}

		/*
		 * The following functions return the value in the Lua stack
		 * index stackPos as the desired type if it can, or null
		 * otherwise.
		 */
		private object getAsSbyte(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			sbyte retVal = (sbyte)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsByte(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			byte retVal = (byte)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsShort(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			short retVal = (short)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsUshort(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			ushort retVal = (ushort)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsInt(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			int retVal = (int)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsUint(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			uint retVal = (uint)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsLong(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			long retVal = (long)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsUlong(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			ulong retVal = (ulong)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsDouble(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			double retVal = KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsChar(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			char retVal = (char)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsFloat(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			float retVal = (float)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsDecimal(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			decimal retVal = (decimal)KopiLua.Lua.lua_tonumber(luaState, stackPos);
			if(retVal == 0 && !KopiLua.Lua.lua_isnumber(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsBoolean(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			return KopiLua.Lua.lua_toboolean(luaState, stackPos);
		}

		private object getAsString(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			string retVal = KopiLua.Lua.lua_tostring(luaState, stackPos).ToString();
			if(retVal == string.Empty && !KopiLua.Lua.lua_isstring(luaState, stackPos).ToBoolean())
				return null;

			return retVal;
		}

		private object getAsTable(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			return translator.getTable(luaState, stackPos);
		}

		private object getAsFunction(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			return translator.getFunction(luaState, stackPos);
		}

		private object getAsUserdata(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			return translator.getUserData(luaState, stackPos);
		}

		public object getAsObject(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			if(KopiLua.Lua.lua_type(luaState, stackPos).ToLuaTypes() == LuaTypes.Table) 
			{
				if(KopiLua.Lua.luaL_getmetafield(luaState, stackPos, "__index").ToBoolean()) 
				{
					if(LuaLib.luaL_checkmetatable(luaState, -1)) 
					{
						KopiLua.Lua.lua_insert(luaState, stackPos);
						KopiLua.Lua.lua_remove(luaState, stackPos+1);
					} 
					else
						KopiLua.Lua.lua_settop(luaState, -2);
				}
			}

			object obj = translator.getObject(luaState, stackPos);
			return obj;
		}

		public object getAsNetObject(KopiLua.Lua.lua_State luaState, int stackPos) 
		{
			object obj = translator.getNetObject(luaState, stackPos);

			if(obj.IsNull() && KopiLua.Lua.lua_type(luaState, stackPos).ToLuaTypes() == LuaTypes.Table) 
			{
				if(KopiLua.Lua.luaL_getmetafield(luaState, stackPos, "__index").ToBoolean()) 
				{
					if(LuaLib.luaL_checkmetatable(luaState, -1)) 
					{
						KopiLua.Lua.lua_insert(luaState, stackPos);
						KopiLua.Lua.lua_remove(luaState, stackPos+1);
						obj = translator.getNetObject(luaState, stackPos);
					} 
					else 
						KopiLua.Lua.lua_settop(luaState, -2);
				}
			}

			return obj;
		}
	}
}