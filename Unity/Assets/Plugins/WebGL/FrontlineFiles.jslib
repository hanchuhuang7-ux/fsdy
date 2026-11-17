mergeInto(LibraryManager.library, {
 FrontlineDownload: function(namePtr,textPtr) {
  var name=UTF8ToString(namePtr),text=UTF8ToString(textPtr);
  var url=URL.createObjectURL(new Blob([text],{type:'application/json;charset=utf-8'}));
  var link=document.createElement('a');link.href=url;link.download=name;document.body.appendChild(link);link.click();link.remove();setTimeout(function(){URL.revokeObjectURL(url)},60000);
 },
 FrontlineUpload: function(receiverPtr) {
  var receiver=UTF8ToString(receiverPtr),input=document.createElement('input');input.type='file';input.accept='.json,application/json';input.style.display='none';document.body.appendChild(input);
  input.onchange=function(){var file=input.files[0];if(!file){input.remove();return}if(file.size>67108864){SendMessage(receiver,'OnFileError','文件超过 64MB');input.remove();return}var reader=new FileReader();reader.onload=function(){SendMessage(receiver,'OnFileLoaded',String(reader.result));input.remove()};reader.onerror=function(){SendMessage(receiver,'OnFileError','无法读取文件');input.remove()};reader.readAsText(file)};
  input.oncancel=function(){input.remove()};input.click();
 }
});
