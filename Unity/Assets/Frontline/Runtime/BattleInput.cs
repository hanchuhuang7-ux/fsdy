using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Frontline.Core;
namespace Frontline.UnityGame
{
    public sealed class BattleInput:MonoBehaviour
    {
        enum MouseIntent {World,Rotate,Pan}
        FrontlineApp app;
        NativeHud hud;
        Vector2 start,last;
        bool down,blocked,dragging,multi,marquee,editedLastFrame;
        int finger=-1,mouseButton=-1;
        MouseIntent intent;
        string mousePhase;
        public void Initialize(FrontlineApp a){app=a;}
        NativeHud Hud => hud ? hud : (hud=app.GetComponent<NativeHud>());
        bool Editing(){var go=EventSystem.current?.currentSelectedGameObject;return go&&go.TryGetComponent<InputField>(out var field)&&field.isFocused;}
        bool OverUi(int pointer=-1){return EventSystem.current&&(pointer<0?EventSystem.current.IsPointerOverGameObject():EventSystem.current.IsPointerOverGameObject(pointer));}
        void Update()
        {
            if(!app||app.View==null)return;
            bool editing=Editing(),escape=Input.GetKeyDown(KeyCode.Escape);
            bool protect=editing || (escape&&editedLastFrame);
            editedLastFrame=editing;
            if(protect){CancelPointer();return;}
            if(escape&&Shortcut(KeyCode.Escape)){CancelPointer();return;}
            if(app.Modal||app.Busy){CancelPointer();return;}
            if(Input.GetKeyDown(KeyCode.R))Shortcut(KeyCode.R);
            if(Input.GetKeyDown(KeyCode.Space))Shortcut(KeyCode.Space);
            if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))Shortcut(KeyCode.Alpha1);
            if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2))Shortcut(KeyCode.Alpha2);
            if(Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Keypad3))Shortcut(KeyCode.Alpha3);
            if(app.Phase=="manor")
            {
                if(Input.GetKeyDown(KeyCode.G))Shortcut(KeyCode.G);
                else if(Input.GetKeyDown(KeyCode.P))Shortcut(KeyCode.P);
                else if(Input.GetKeyDown(KeyCode.M))Shortcut(KeyCode.M);
                if(app.Modal){CancelPointer();return;}
            }
            else
            {
                if(Input.GetKeyDown(KeyCode.Tab)){app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();}
                if((Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)||Input.GetKey(KeyCode.LeftCommand)||Input.GetKey(KeyCode.RightCommand))&&Input.GetKeyDown(KeyCode.Z))app.Undo();
                if(Input.GetKeyDown(KeyCode.Delete)||Input.GetKeyDown(KeyCode.Backspace))app.DeleteSelection();
            }
            float x=(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0)-(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0);
            float y=(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0)-(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0);
            if(x!=0||y!=0)app.View.Pan(new Vector2(x,y).normalized*(Screen.height*.55f*Mathf.Min(Time.unscaledDeltaTime,.08f)));
            if(Input.touchCount>0){if(mouseButton>=0)CancelPointer();app.View.HoveredPlot=-1;app.View.Marker=null;TouchInput();return;}

            Vector2 p=Input.mousePosition;
            bool over=OverUi();
            if(!over&&Mathf.Abs(Input.mouseScrollDelta.y)>.001f)app.View.Zoom(Input.mouseScrollDelta.y);
            if(mouseButton<0)
                for(int i=0;i<3;i++)if(Input.GetMouseButtonDown(i))
                {BeginMouse(i,p,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift),over);break;}
            if(mouseButton>=0)
            {
                if(Input.GetMouseButtonUp(mouseButton))EndMouse(p,over);
                else if(Input.GetMouseButton(mouseButton))MoveMouse(p);
            }
            HoverAt(p,over||!Application.isFocused||!Input.mousePresent||Application.isMobilePlatform);
            if(app.Phase!="manor"&&!over&&!dragging&&app.View.Ground(p,out var ground))
            {
                bool order=app.Config.commander.enabled&&app.AllowsOrder(app.OrderType)&&
                    (app.Phase=="battle"||app.InFrontier&&app.Tool=="order");
                app.View.Marker=ground;
                app.View.MarkerOrder=order?app.OrderType:null;
                app.View.MarkerRadius=order?(app.OrderType=="focus"?4:6):.55f;
            }
            else app.View.Marker=null;
        }
        internal bool Shortcut(KeyCode key)
        {
            if(Editing())return false;
            if(app.Modal)return key==KeyCode.Escape&&Hud.TryDismissDialog();
            if(app.Busy)return false;
            if(key==KeyCode.R){app.View.ResetCamera();return true;}
            if(key==KeyCode.Space)
            {
                if(app.Phase=="manor")app.SetManorSpeed(app.ManorSpeed==0?1:0);
                else app.TogglePause();
                return true;
            }
            if(app.Phase=="manor"&&Hud.TryManorShortcut(key))return true;
            int index=(int)key-(int)KeyCode.Alpha1;
            if(index<0||index>=Catalog.Orders.Length)return false;
            if(app.Phase=="manor")app.View.FrameFrontier(index,true);
            else app.ChooseOrder(Catalog.Orders[index]);
            return true;
        }
        internal void HoverAt(Vector2 point,bool overUi)
        {
            int plot=-1;
            if(app.Phase=="manor"&&!Viewport.Compact&&!overUi&&mouseButton<0&&!app.Modal&&!app.Busy&&
                point.x>=0&&point.x<Screen.width&&point.y>=0&&point.y<Screen.height&&app.View.Ground(point,out var ground))
                plot=Manor.PickSlot(ground.x-FrontierMap.ManorX,ground.z);
            app.View.HoveredPlot=plot;
        }

        internal void BeginMouse(int button,Vector2 p,bool shift,bool overUi)
        {
            start=last=p;mouseButton=button;mousePhase=app.Phase;blocked=overUi;dragging=false;
            app.View.HoveredPlot=-1;
            if(button==2||button==0&&shift)intent=MouseIntent.Pan;
            else if(button==1||Viewport.Compact&&app.TouchCamera)intent=MouseIntent.Rotate;
            else intent=MouseIntent.World;
        }
        internal void MoveMouse(Vector2 p)
        {
            if(mouseButton<0||blocked||mousePhase!=app.Phase)return;
            if(!dragging && (p-start).sqrMagnitude<=64)return;
            dragging=true;
            var delta=p-last;last=p;
            if(intent==MouseIntent.Pan)app.View.Pan(delta);
            else if(intent==MouseIntent.Rotate)app.View.Orbit(delta);
            else if(app.CanEdit&&app.Tool=="select")
            {
                if(!hud)hud=app.GetComponent<NativeHud>();
                hud.SelectionBox(start,p,true);marquee=true;
            }
        }
        internal void EndMouse(Vector2 p,bool overUi)
        {
            MoveMouse(p);
            bool world=mouseButton==0&&!blocked&&!overUi&&mousePhase==app.Phase&&
                (intent==MouseIntent.World||intent==MouseIntent.Rotate&&app.Phase=="manor"&&!dragging);
            bool box=world&&dragging&&app.CanEdit&&app.Tool=="select";
            bool click=world&&!dragging;
            CancelPointer();
            if(box)app.SelectRectangle(start,p);
            else if(click)Click(p);
        }
        void CancelPointer()
        {
            if(marquee&&hud)hud.SelectionBox(start,last,false);
            if(app&&app.View){app.View.HoveredPlot=-1;app.View.Marker=null;}
            marquee=false;mouseButton=-1;finger=-1;down=false;dragging=false;
        }
        void OnApplicationFocus(bool focus){if(!focus)CancelPointer();}
        void OnDisable(){CancelPointer();}
        void TouchInput()
        {
            if(Input.touchCount>1){multi=true;down=false;if(Input.touchCount==2){var a=Input.GetTouch(0);var b=Input.GetTouch(1);float before=(a.position-a.deltaPosition-(b.position-b.deltaPosition)).magnitude;float now=(a.position-b.position).magnitude;if(!OverUi(a.fingerId)&&!OverUi(b.fingerId)){app.View.Zoom((now-before)*.02f);app.View.Pan((a.deltaPosition+b.deltaPosition)*.5f);}}return;}
            var t=Input.GetTouch(0);if(t.phase==TouchPhase.Began){finger=t.fingerId;start=last=t.position;down=true;dragging=false;multi=false;blocked=OverUi(finger);}
            if(t.fingerId!=finger||!down)return;if((t.position-start).magnitude>12)dragging=true;
            if(t.phase==TouchPhase.Moved&&!blocked&&(app.TouchCamera||app.Phase=="manor"))app.View.Orbit(t.position-last);last=t.position;
            if(t.phase==TouchPhase.Ended){if(!blocked&&!multi&&!dragging&&(!app.TouchCamera||app.Phase=="manor"))Click(t.position);down=false;finger=-1;}else if(t.phase==TouchPhase.Canceled){down=false;finger=-1;}
        }
        internal void Click(Vector2 p){if(!app.View.Ground(p,out var hit))return;if(app.Phase=="manor")app.SelectManorSlot(Manor.PickSlot(hit.x-FrontierMap.ManorX,hit.z));else if(app.InFrontier)app.ClickFrontier(hit);else if(app.Phase=="battle")app.CommandAt(hit);else if(app.CanEdit)app.PlaceAt(hit);}
    }
}

