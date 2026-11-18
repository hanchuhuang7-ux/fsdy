using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Frontline.UnityGame
{
    public sealed class BattleInput:MonoBehaviour
    {
        FrontlineApp app;Vector2 start,last;bool down,blocked,dragging,multi;int finger=-1;
        public void Initialize(FrontlineApp a){app=a;}
        bool Editing(){var go=EventSystem.current?.currentSelectedGameObject;return go&&go.GetComponent<InputField>()!=null;}
        bool OverUi(int pointer=-1){return EventSystem.current&&(pointer<0?EventSystem.current.IsPointerOverGameObject():EventSystem.current.IsPointerOverGameObject(pointer));}
        void Update()
        {
            if(!app||app.View==null)return;if(!app.Modal&&!Editing()&&!app.Busy&&app.Phase!="manor"){if(Input.GetKeyDown(KeyCode.Space))app.TogglePause();if(Input.GetKeyDown(KeyCode.Alpha1)){app.OrderType="inspire";app.Refresh();}if(Input.GetKeyDown(KeyCode.Alpha2)&&app.AllowsOrder("focus")){app.OrderType="focus";app.Refresh();}if(Input.GetKeyDown(KeyCode.Alpha3)&&app.AllowsOrder("fortify")){app.OrderType="fortify";app.Refresh();}if(Input.GetKeyDown(KeyCode.Tab)){app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();}if((Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)||Input.GetKey(KeyCode.LeftCommand)||Input.GetKey(KeyCode.RightCommand))&&Input.GetKeyDown(KeyCode.Z))app.Undo();if(Input.GetKeyDown(KeyCode.Delete)||Input.GetKeyDown(KeyCode.Backspace))app.DeleteSelection();}
            if(app.Modal||app.Busy){down=false;return;}
            if(Input.touchCount>0){TouchInput();return;}
            bool manor=app.Phase=="manor";Vector2 p=Input.mousePosition;bool camera=manor||Input.GetMouseButton(1)||Input.GetMouseButton(2)||app.TouchCamera&&Viewport.Compact;
            if(!OverUi()&&Mathf.Abs(Input.mouseScrollDelta.y)>.001f)app.View.Zoom(Input.mouseScrollDelta.y);
            if(Input.GetMouseButtonDown(0)||Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2)){start=last=p;down=true;dragging=false;blocked=OverUi();}
            if(down&&!blocked&&(Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2))){if((p-start).magnitude>8)dragging=true;if(camera&&dragging)app.View.Orbit(p-last);last=p;}
            if(down&&(Input.GetMouseButtonUp(0)||Input.GetMouseButtonUp(1)||Input.GetMouseButtonUp(2))){if(!blocked&&manor&&!dragging&&Input.GetMouseButtonUp(0))Click(p);else if(!blocked&&!camera){if(dragging&&app.CanEdit&&app.Tool=="select")app.SelectRectangle(start,p);else if(!dragging)Click(p);}down=false;}
            if(!manor&&!OverUi()&&app.View.Ground(p,out var ground)){app.View.Marker=ground;app.View.MarkerRadius=app.Phase=="battle"?app.OrderType=="focus"?4:6:.55f;}else app.View.Marker=null;
        }
        void TouchInput()
        {
            if(Input.touchCount>1){multi=true;down=false;if(Input.touchCount==2){var a=Input.GetTouch(0);var b=Input.GetTouch(1);float before=(a.position-a.deltaPosition-(b.position-b.deltaPosition)).magnitude;float now=(a.position-b.position).magnitude;if(!OverUi(a.fingerId)&&!OverUi(b.fingerId))app.View.Zoom((now-before)*.02f);}return;}
            var t=Input.GetTouch(0);if(t.phase==TouchPhase.Began){finger=t.fingerId;start=last=t.position;down=true;dragging=false;multi=false;blocked=OverUi(finger);}
            if(t.fingerId!=finger||!down)return;if((t.position-start).magnitude>12)dragging=true;
            if(t.phase==TouchPhase.Moved&&!blocked&&(app.TouchCamera||app.Phase=="manor"))app.View.Orbit(t.position-last);last=t.position;
            if(t.phase==TouchPhase.Ended){if(!blocked&&!multi&&!dragging&&(!app.TouchCamera||app.Phase=="manor"))Click(t.position);down=false;finger=-1;}else if(t.phase==TouchPhase.Canceled){down=false;finger=-1;}
        }
        internal void Click(Vector2 p){if(!app.View.Ground(p,out var hit))return;if(app.Phase=="manor")app.SelectManorSlot(Frontline.Core.Manor.PickSlot(hit.x,hit.z));else if(app.Phase=="battle")app.CommandAt(hit);else if(app.CanEdit)app.PlaceAt(hit);}
    }
}

