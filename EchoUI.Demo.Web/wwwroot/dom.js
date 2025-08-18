const elementRegistry = new Map();

async function handleEvent(e, elementId) {
    let eventArgs = {};
    const eventType = e.type;

    console.log('eventType', eventType);
    // 根据事件类型，构建要发送到 C# 的参数
    if (eventType === 'mousemove' || eventType === 'mousedown' || eventType === 'mouseup') {
        eventArgs = { X: e.offsetX, Y: e.offsetY, Button: e.button };
    } else if (eventType === 'click') {
        // 对于click事件，我们更关心是哪个按钮触发的
        eventArgs = e.button; // 0=左键, 1=中键, 2=右键
    } else if (eventType === 'keydown' || eventType === 'keyup') {
        eventArgs = e.keyCode;
    } else if (eventType === 'input') {
        // 对于Input组件的值变化
        eventArgs = e.target.value;
    }

    try {
        await window.EchoUIHelper.RaiseEventAsync(elementId, eventType, JSON.stringify(eventArgs));
    } catch (error) {
        console.error(`Error invoking .NET method for event '${eventType}' on element '${elementId}':`, error);
    }
}

function getElement(elementId) {
    return elementRegistry.get(elementId) || document.getElementById(elementId);
}

export const dom = {
    createElement: (elementId, type) => {
        console.log('createElement');
        const el = document.createElement(type);
        el._eventListeners = {};
        elementRegistry.set(elementId, el);
        return el;
    },

    patchProperties: (elementId, patchJson) => {
        const el = getElement(elementId);
        if (!el) return;

        console.log(patchJson);
        const patch = JSON.parse(patchJson);

        if (patch.styles) {
            for (const [key, value] of Object.entries(patch.styles)) {
                el.style[key] = value;
            }
        }

        if (patch.attributes) {
            for (const [key, value] of Object.entries(patch.attributes)) {
                if (key in el) {
                    el[key] = value;
                } else {
                    el.setAttribute(key, value);
                }
            }
        }

        const currentListeners = el._eventListeners || {};
        let isChangeListeners = false;

        if (patch.eventsToRemove) {
            for (const eventName of patch.eventsToRemove) {
                if (currentListeners[eventName]) {
                    el.removeEventListener(eventName, currentListeners[eventName]);
                    delete currentListeners[eventName];
                }
            }
            isChangeListeners = true;
        }

        if (patch.eventsToAdd) {
            for (const eventName of patch.eventsToAdd) {
                if (!currentListeners[eventName]) {
                    const handler = (e) => handleEvent(e, elementId);
                    el.addEventListener(eventName, handler);
                    currentListeners[eventName] = handler;
                }
            }
            isChangeListeners = true;
        }

        if (isChangeListeners)
            el._eventListeners = currentListeners;
    },

    addChild: (parentId, childId, index) => {
        console.log('addChild');
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (!parent || !child) return;
        const referenceNode = parent.children[index] || null;
        parent.insertBefore(child, referenceNode);
    },

    removeChild: (parentId, childId) => {
        console.log('removeChild');
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (parent && child && child.parentElement === parent) {
            parent.removeChild(child);
            elementRegistry.delete(childId);
        }
    },

    moveChild: (parentId, childId, newIndex) => {
        console.log('moveChild');
        const parent = getElement(parentId);
        const child = getElement(childId);
        if (parent && child) {
            const referenceNode = parent.children[newIndex] || null;
            parent.insertBefore(child, referenceNode);
        }
    }
};